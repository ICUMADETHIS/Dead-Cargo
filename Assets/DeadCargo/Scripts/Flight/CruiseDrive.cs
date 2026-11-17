using System;
using DeadCargo.Sectors;
using DeadCargo.Sectors.Far;
using DeadCargo.Visuals;
using Unity.Mathematics;
using UnityEngine;

namespace DeadCargo.Flight
{
    /// <summary>
    /// The player's cruise drive: crosses the star system to a planet, and back, in under a minute.
    /// <para>
    /// <b>Engaging.</b> <see cref="Toggle"/> charges the drive, then the ship cruises where its nose points while the
    /// player steers as usual. Speed follows the space around the ship (<see cref="CruiseMath.TargetSpeed"/>): a fraction
    /// of the distance to the nearest surface, a planet's or the sector's edge.
    /// </para>
    /// <para>
    /// <b>Near and away.</b> Inside the sector and out to <c>homeMargin</c> past its edge, the ship really flies through the
    /// near world, so the rocks stream past. Beyond that, past the camera's draw distance, the ship and camera are moved
    /// back to the world's origin, the sector's <see cref="SectorSpace.SystemOffset"/> takes up the difference and the
    /// sector sleeps (<see cref="SectorPresence"/>). Away, the ship holds still in the world and the offset carries it
    /// through the system, so the far world, and the planets in it, come closer. Coming home reverses it.
    /// </para>
    /// <para>
    /// <b>Dropping out.</b> On <see cref="Toggle"/>, at a planet's drop height, on arriving home, or on a collision.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CruiseDrive : MonoBehaviour
    {
        /// <summary><see cref="HeadingIndex"/> when the sector is the destination ahead.</summary>
        public const int HomeHeading = -1;

        /// <summary><see cref="HeadingIndex"/> when nothing lies ahead.</summary>
        public const int NoHeading = -2;

        // A destination counts as ahead within this angle of the nose, in degrees.
        private const float HeadingCone = 30f;

        // Away and out of cruise, the ship is moved back to the world's origin when it strays this far, in metres.
        private const float RecentreDistance = 20000f;

        [Header("Engage")]
        [Tooltip("Seconds the drive charges before it cruises. Pressing again cancels.")]
        [SerializeField, Min(0f)] private float chargeTime = 3f;

        [Header("Speed")]
        [Tooltip("Cruise speed for each metre to the nearest surface, per second. 0.2 closes a fifth of the gap each second, " +
                 "so every trip takes about as long whatever its length.")]
        [SerializeField, Range(0.01f, 2f)] private float rate = 0.2f;

        [Tooltip("Slowest cruise, in metres a second.")]
        [SerializeField, Min(1f)] private float minSpeed = 2500f;

        [Tooltip("Fastest cruise, in metres a second.")]
        [SerializeField, Min(1f)] private float maxSpeed = 400000f;

        [Tooltip("How quickly the speed follows its target, per second. Lower eases in and out more gently.")]
        [SerializeField, Range(0.1f, 10f)] private float response = 1.5f;

        [Header("Drop Out")]
        [Tooltip("Height above a planet's sea level where the drive drops out, in metres: 3 km comes out under Ixara's " +
                 "clouds with the ground in view. Below it the drive will not engage.")]
        [SerializeField, Min(0f)] private float dropHeight = 3000f;

        [Tooltip("Distance past the sector's edge where it sleeps on the way out and wakes on the way home, in metres. " +
                 "At the camera's draw distance, so nothing visible changes.")]
        [SerializeField, Min(0f)] private float homeMargin = 10000f;

        [Tooltip("Speed the ship keeps on leaving cruise, in metres a second.")]
        [SerializeField, Min(0f)] private float exitSpeed = 300f;

        [Header("Look")]
        [Tooltip("Fastest the space dust streams past while the ship holds still in cruise, in metres a second. Much " +
                 "faster strobes.")]
        [SerializeField, Min(0f)] private float dustSpeed = 1500f;

        private Rigidbody body;
        private SectorSpace space;
        private SectorPresence presence;
        private RigidbodyConstraints freeConstraints;
        private float freeDamping;
        private bool holding;

        /// <summary>Raised when charging starts.</summary>
        public event Action ChargeStarted;

        /// <summary>Raised when the player cancels a charge.</summary>
        public event Action ChargeCancelled;

        /// <summary>Raised when the drive starts cruising.</summary>
        public event Action Engaged;

        /// <summary>Raised when cruise ends, with why.</summary>
        public event Action<CruiseExit> Dropped;

        public CruiseState State { get; private set; }

        /// <summary>Seconds of charge left.</summary>
        public float ChargeRemaining { get; private set; }

        /// <summary>How far the charge has run, 0 to 1.</summary>
        public float ChargeProgress => chargeTime > 0f ? Mathf.Clamp01(1f - ChargeRemaining / chargeTime) : 1f;

        /// <summary>Cruise speed, in metres a second. 0 out of cruise.</summary>
        public double Speed { get; private set; }

        /// <summary>Distance to the nearest surface, a planet's or the sector's edge, in metres.</summary>
        public double Clearance { get; private set; }

        /// <summary>The planet whose surface is nearest, or -1 when the sector's edge is nearer.</summary>
        public int NearestPlanet { get; private set; } = -1;

        /// <summary>
        /// The destination nearest the nose, within 30°: a planet's index in <see cref="BackdropBodies"/>,
        /// <see cref="HomeHeading"/> for the sector, or <see cref="NoHeading"/>.
        /// </summary>
        public int HeadingIndex { get; private set; } = NoHeading;

        /// <summary>Height above the heading destination's surface, or distance to the sector's edge, in metres.</summary>
        public double HeadingDistance { get; private set; }

        /// <summary>True while the player is away from the sector, which sleeps.</summary>
        public bool IsAway => presence != null && presence.IsAsleep;

        /// <summary>True when the ship is below a planet's drop height, where the drive will not engage.</summary>
        public bool IsBelowFloor { get; private set; }

        /// <summary>The ship's place in the star system, in metres.</summary>
        public double3 SystemPosition => space != null ? space.WorldToSystem(body.position) : (double3)(float3)body.position;

        /// <summary>True when the drive can start charging now.</summary>
        public bool CanEngage => State == CruiseState.Ready && space != null && !IsBelowFloor;

        /// <summary>The rules as plain values.</summary>
        public CruiseSettings Settings => new CruiseSettings
        {
            Rate = rate,
            MinSpeed = minSpeed,
            MaxSpeed = maxSpeed,
            Response = response,
            DropHeight = dropHeight
        };

        /// <summary>Starts charging, cancels a charge, or drops out of cruise, as the drive's one control does.</summary>
        public void Toggle()
        {
            switch (State)
            {
                case CruiseState.Ready:
                    if (CanEngage)
                    {
                        State = CruiseState.Charging;
                        ChargeRemaining = chargeTime;
                        ChargeStarted?.Invoke();
                    }

                    break;
                case CruiseState.Charging:
                    State = CruiseState.Ready;
                    ChargeRemaining = 0f;
                    ChargeCancelled?.Invoke();
                    break;
                case CruiseState.Cruising:
                    DropOut(CruiseExit.Player);
                    break;
            }
        }

        /// <summary>True when a place in the star system is within reach of the sector, where it is awake.</summary>
        public bool IsHome(double3 systemPosition)
        {
            return space == null || DistanceToSector(systemPosition) <= homeMargin;
        }

        /// <summary>
        /// Puts the ship at a place in the star system at once, facing <paramref name="facing"/> with its top towards
        /// <paramref name="up"/>, at rest: in the sector, awake, or away, with the sector asleep. For debug tools.
        /// </summary>
        public void JumpTo(double3 systemPosition, Vector3 facing, Vector3 up)
        {
            if (space == null)
                return;

            if (State == CruiseState.Cruising)
                DropOut(CruiseExit.Player);

            State = CruiseState.Ready;
            if (IsHome(systemPosition))
            {
                if (presence != null)
                    presence.Wake();

                space.SetSystemOffset(double3.zero);
                MoveRig(space.SystemToWorld(systemPosition) - body.position);
            }
            else
            {
                if (presence != null)
                    presence.Sleep();

                MoveRig(-body.position);
                space.SetSystemOffset(systemPosition - (double3)(float3)space.WorldToSector(Vector3.zero));
            }

            if (facing.sqrMagnitude > 1e-6f)
            {
                Quaternion turn = Quaternion.LookRotation(facing.normalized, up.sqrMagnitude > 1e-6f ? up : Vector3.up);
                body.rotation = turn;
                transform.rotation = turn;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            Survey();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            freeConstraints = body.constraints;
        }

        private void OnDisable()
        {
            if (State == CruiseState.Cruising)
                DropOut(CruiseExit.Interrupted);

            State = CruiseState.Ready;
        }

        private void Update()
        {
            if (space == null)
                space = SectorSpace.Active;
            if (presence == null)
                presence = SectorPresence.Active;

            Survey();
            switch (State)
            {
                case CruiseState.Charging:
                    ChargeRemaining -= Time.deltaTime;
                    if (ChargeRemaining <= 0f)
                        Engage();
                    break;
                case CruiseState.Cruising:
                    Cruise(Time.deltaTime);
                    break;
                default:
                    if (IsAway && body.position.sqrMagnitude > RecentreDistance * RecentreDistance)
                        Recentre();
                    break;
            }

            SpaceDust dust = SpaceDust.Active;
            if (dust != null)
                dust.Drift(holding ? transform.forward * (float)Math.Min(Speed, dustSpeed) : Vector3.zero);
        }

        // Near the sector the ship really moves; physics carries it, smoothed by the rigidbody's interpolation.
        private void FixedUpdate()
        {
            if (State == CruiseState.Cruising && !holding)
                body.linearVelocity = transform.forward * (float)Speed;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (State == CruiseState.Cruising)
                DropOut(CruiseExit.Collision);
        }

        private void Engage()
        {
            State = CruiseState.Cruising;
            ChargeRemaining = 0f;
            Speed = Math.Max(0.0, Vector3.Dot(body.linearVelocity, transform.forward));

            // The kit's engines fly against heavy drag, which would eat into the cruise speed each physics step.
            freeDamping = body.linearDamping;
            body.linearDamping = 0f;
            if (IsAway)
            {
                Recentre();
                Hold(true);
            }

            Engaged?.Invoke();
        }

        private void Cruise(float deltaTime)
        {
            CruiseSettings settings = Settings;
            Speed = CruiseMath.Ease(Speed, CruiseMath.TargetSpeed(Clearance, settings), settings.Response, deltaTime);
            double3 position = SystemPosition;

            if (!holding)
            {
                // Near the sector: the rigidbody moves. Past its reach, the sector sleeps and the ship holds still.
                if (!IsHome(position))
                    Depart();

                return;
            }

            double3 forward = (double3)(float3)transform.forward;
            double step = Speed * deltaTime;
            bool reachedFloor = ClampToFloors(position, forward, ref step);

            space.SetSystemOffset(space.SystemOffset + forward * step);
            if (reachedFloor)
                DropOut(CruiseExit.Planet);
            else if (IsHome(position + forward * step))
                Arrive();
        }

        // Shortens the step so it ends at the first planet's drop height in its path. True when it does.
        private bool ClampToFloors(double3 position, double3 forward, ref double step)
        {
            BackdropBodies bodies = BackdropBodies.Active;
            if (bodies == null)
                return false;

            bool reached = false;
            for (int i = 0; i < bodies.Plans.Count; ++i)
            {
                if (!bodies.IsBuilt(i))
                    continue;

                double distance = CruiseMath.DistanceToSphere(position, forward, bodies.CentreOf(i), bodies.RadiusOf(i) + dropHeight);
                if (distance <= step)
                {
                    step = distance;
                    reached = true;
                }
            }

            return reached;
        }

        // From flying through the sector to holding still while the system moves: the ship and camera go back to the
        // world's origin, and the offset takes up the difference, so the ship's place in the system does not change.
        private void Depart()
        {
            if (presence != null)
                presence.Sleep();

            Recentre();
            Hold(true);
        }

        // The ship is put back at its true place in the sector, which wakes, and the drive drops out.
        private void Arrive()
        {
            Vector3 offset = (float3)space.SystemOffset;
            space.SetSystemOffset(double3.zero);
            MoveRig(offset);
            if (presence != null)
                presence.Wake();

            DropOut(CruiseExit.Home);
        }

        private void Recentre()
        {
            Vector3 position = body.position;
            if (position == Vector3.zero)
                return;

            MoveRig(-position);
            space.SetSystemOffset(space.SystemOffset + (double3)(float3)position);
        }

        private void DropOut(CruiseExit exit)
        {
            if (State != CruiseState.Cruising)
                return;

            State = CruiseState.Ready;
            Hold(false);
            body.linearDamping = freeDamping;
            body.linearVelocity = transform.forward * exitSpeed;
            Speed = 0.0;
            Dropped?.Invoke(exit);
        }

        private void Hold(bool hold)
        {
            if (hold == holding)
                return;

            holding = hold;
            if (hold)
            {
                freeConstraints = body.constraints;
                body.linearVelocity = Vector3.zero;
                body.constraints = freeConstraints | RigidbodyConstraints.FreezePosition;
            }
            else
            {
                body.constraints = freeConstraints;
            }
        }

        // The ship and the camera rig move together, so the camera does not chase the ship across the jump.
        private void MoveRig(Vector3 offset)
        {
            Transform ship = transform.root;
            RigShift.Move(ship, offset);

            Camera view = Camera.main;
            Transform rig = view != null ? view.transform.root : null;
            if (rig != null && rig != ship)
                RigShift.Move(rig, offset);
        }

        // Clearance, the nearest planet, the destination ahead and whether the ship is below a drop height.
        private void Survey()
        {
            if (space == null)
                return;

            double3 position = SystemPosition;
            double3 forward = (double3)(float3)transform.forward;
            double clearance = DistanceToSector(position);
            int nearest = -1;
            bool belowFloor = false;

            int heading = NoHeading;
            double headingDistance = 0.0;
            float bestAngle = HeadingCone;
            if (IsAway)
                ConsiderHeading(GridCentre() - position, HomeHeading, clearance, ref heading, ref headingDistance, ref bestAngle);

            BackdropBodies bodies = BackdropBodies.Active;
            if (bodies != null)
            {
                for (int i = 0; i < bodies.Plans.Count; ++i)
                {
                    if (!bodies.IsBuilt(i))
                        continue;

                    double3 centre = bodies.CentreOf(i);
                    double altitude = CruiseMath.Altitude(position, centre, bodies.RadiusOf(i));
                    if (altitude < clearance)
                    {
                        clearance = altitude;
                        nearest = i;
                    }

                    belowFloor |= altitude < dropHeight;
                    ConsiderHeading(centre - position, i, altitude, ref heading, ref headingDistance, ref bestAngle);
                }
            }

            Clearance = Math.Max(0.0, clearance);
            NearestPlanet = nearest;
            IsBelowFloor = belowFloor;
            HeadingIndex = heading;
            HeadingDistance = Math.Max(0.0, headingDistance);

            void ConsiderHeading(double3 toTarget, int index, double distance, ref int best, ref double bestDistance, ref float angleLimit)
            {
                float angle = Vector3.Angle(transform.forward, (float3)toTarget);
                if (angle > angleLimit)
                    return;

                angleLimit = angle;
                best = index;
                bestDistance = distance;
            }
        }

        private double DistanceToSector(double3 systemPosition)
        {
            SectorGrid grid = space.Grid;
            double3 min = (double3)(float3)grid.Min;
            return CruiseMath.DistanceToBox(systemPosition, min, min + (double3)(float3)grid.Size);
        }

        private double3 GridCentre()
        {
            SectorGrid grid = space.Grid;
            return (double3)(float3)(grid.Min + grid.Size * 0.5f);
        }
    }
}
