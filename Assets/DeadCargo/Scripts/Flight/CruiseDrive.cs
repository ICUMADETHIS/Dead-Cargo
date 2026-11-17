using System;
using System.Collections.Generic;
using DeadCargo.Sectors;
using DeadCargo.Sectors.Far;
using DeadCargo.Visuals;
using Unity.Mathematics;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Flight
{
    /// <summary>
    /// The player's cruise drive, an autopilot: point at a planet or, while away, at the sector, and engage. The drive takes
    /// the controls, turns the ship to its course while it charges, flies there itself, and hands the controls back where
    /// cruise ends.
    /// <para>
    /// <b>Engaging.</b> <see cref="Toggle"/> locks the destination nearest the nose, within 30°, and charges. It is refused
    /// with nothing ahead, at the destination already, or with an asteroid field along the route out of the sector
    /// (<see cref="Block"/>). Pressing again cancels the charge, or in cruise drops out.
    /// </para>
    /// <para>
    /// <b>The flight.</b> Course and speed follow <see cref="CruiseAutopilot"/>: straight at the destination, round any
    /// planet in the way, fast in open space and braking into the arrival.
    /// </para>
    /// <para>
    /// <b>Near and away.</b> Within the sector's reach the ship really flies through the near world, so the rocks stream
    /// past. Beyond it, past the camera's draw distance, the ship and camera are moved back to the world's origin, the
    /// sector's <see cref="SectorSpace.SystemOffset"/> takes up the difference and the sector sleeps
    /// (<see cref="SectorPresence"/>). Away, the ship holds still in the world and the offset carries it through the system.
    /// Coming home reverses it.
    /// </para>
    /// <para>
    /// <b>Arriving.</b> At a planet, cruise ends above the top of its air, nose down; at the sector, at its reach. A
    /// collision, or <see cref="Toggle"/>, ends it early.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CruiseDrive : MonoBehaviour
    {
        /// <summary><see cref="HeadingIndex"/> or <see cref="Destination"/> when it is the sector.</summary>
        public const int HomeHeading = CruiseAutopilot.Home;

        /// <summary><see cref="HeadingIndex"/> or <see cref="Destination"/> when there is none.</summary>
        public const int NoHeading = CruiseAutopilot.None;

        // A destination counts as ahead within this angle of the nose, in degrees.
        private const float HeadingCone = 30f;

        // Away and out of cruise, the ship is moved back to the world's origin when it strays this far, in metres.
        private const float RecentreDistance = 20000f;

        // While ready in the sector, the route out is checked for fields this often, in seconds, at these steps, in metres.
        private const float RouteInterval = 0.2f;
        private const float RouteSpacing = 50f;

        // Cruise that would meet a surface stops this short of it, in metres.
        private const double SurfaceGap = 100.0;

        [Header("Engage")]
        [Tooltip("Seconds the drive charges before it cruises, while the autopilot turns the ship to its course. Pressing " +
                 "again cancels.")]
        [SerializeField, Min(0f)] private float chargeTime = 3f;

        [Tooltip("The autopilot cruises once the charge has run and the nose is this close to its course, in degrees.")]
        [SerializeField, Range(0.1f, 10f)] private float alignTolerance = 2f;

        [Tooltip("Asteroid field density along the route out of the sector above which the drive will not engage, 0 to 1. " +
                 "Lower keeps further from the thin edges of fields.")]
        [SerializeField, Range(0f, 1f)] private float fieldLimit = 0.05f;

        [Header("Autopilot")]
        [Tooltip("Fastest the autopilot turns the ship, in degrees a second.")]
        [SerializeField, Range(10f, 360f)] private float turnRate = 90f;

        [Tooltip("How a turn settles, per second: near the end it slows to this times the angle left.")]
        [SerializeField, Range(0.5f, 10f)] private float turnEase = 3f;

        [Header("Speed")]
        [Tooltip("Speed towards a surface for each metre to it, per second. 0.2 leaves five seconds to reach it, so the " +
                 "drive brakes smoothly into every arrival. Lower makes every trip longer.")]
        [SerializeField, Range(0.01f, 2f)] private float approachRate = 0.2f;

        [Tooltip("Speed for each metre of clearance while not closing on a surface, per second: leaving a planet or the " +
                 "sector, or passing one by. Higher leaves faster.")]
        [SerializeField, Range(0.05f, 5f)] private float crossRate = 1f;

        [Tooltip("Slowest cruise outside the sector's reach, in metres a second: the speed of the last stretch to an arrival.")]
        [SerializeField, Min(1f)] private float slowestSpeed = 2000f;

        [Tooltip("Fastest cruise, in metres a second.")]
        [SerializeField, Min(1f)] private float maxSpeed = 400000f;

        [Tooltip("Cruise speed within the sector's reach, where the ship really flies past rocks, in metres a second.")]
        [SerializeField, Min(1f)] private float sectorSpeed = 4000f;

        [Tooltip("How quickly the speed follows its target, per second. Lower eases in and out more gently.")]
        [SerializeField, Range(0.1f, 10f)] private float response = 1.5f;

        [Header("Arrival")]
        [Tooltip("Height above the top of a planet's air where cruise ends, in metres.")]
        [SerializeField, Min(0f)] private float arrivalMargin = 5000f;

        [Tooltip("Height above an airless planet's surface where cruise ends, in metres.")]
        [SerializeField, Min(0f)] private float airlessArrival = 20000f;

        [Tooltip("Distance past the sector's edge where it sleeps on the way out and wakes on the way home, in metres. " +
                 "At the camera's draw distance, so nothing visible changes.")]
        [SerializeField, Min(0f)] private float homeMargin = 10000f;

        [Tooltip("Speed the ship keeps on leaving cruise, in metres a second.")]
        [SerializeField, Min(0f)] private float exitSpeed = 300f;

        [Header("Look")]
        [Tooltip("Fastest the space dust streams past while the ship holds still in cruise, in metres a second. Much " +
                 "faster strobes.")]
        [SerializeField, Min(0f)] private float dustSpeed = 1500f;

        private readonly List<CruiseBody> bodies = new List<CruiseBody>();

        private Rigidbody body;
        private Engines engines;
        private AtmosphereFlight air;
        private SectorSpace space;
        private SectorPresence presence;
        private RigidbodyConstraints freeConstraints;
        private float freeDamping;
        private bool holding;
        private bool controlsTaken;
        private bool controlsBefore;
        private Vector3 throttleBefore;
        private float routeClock;

        /// <summary>Raised when charging starts.</summary>
        public event Action ChargeStarted;

        /// <summary>Raised when the player cancels a charge.</summary>
        public event Action ChargeCancelled;

        /// <summary>Raised when the drive will not engage, with why.</summary>
        public event Action<CruiseBlock> Refused;

        /// <summary>Raised when the drive starts cruising.</summary>
        public event Action Engaged;

        /// <summary>Raised when cruise ends, with why. <see cref="Destination"/> still names where it was going.</summary>
        public event Action<CruiseExit> Dropped;

        public CruiseState State { get; private set; }

        /// <summary>Seconds of charge left.</summary>
        public float ChargeRemaining { get; private set; }

        /// <summary>How far the charge has run, 0 to 1.</summary>
        public float ChargeProgress => chargeTime > 0f ? Mathf.Clamp01(1f - ChargeRemaining / chargeTime) : 1f;

        /// <summary>True while the charge has run but the autopilot is still turning the ship to its course.</summary>
        public bool IsAligning => State == CruiseState.Charging && ChargeRemaining <= 0f;

        /// <summary>Cruise speed, in metres a second. 0 out of cruise.</summary>
        public double Speed { get; private set; }

        /// <summary>Distance to the nearest surface, a planet's or the sector's edge, in metres.</summary>
        public double Clearance { get; private set; }

        /// <summary>The planet whose surface is nearest, or -1 when the sector's edge is nearer.</summary>
        public int NearestPlanet { get; private set; } = -1;

        /// <summary>True inside the nearest planet's arrival height, where cruise to it would end: in its atmosphere.</summary>
        public bool IsLow { get; private set; }

        /// <summary>The nearest planet's arrival height above its sea level, in metres. 0 with the sector's edge nearer.</summary>
        public double NearestArrivalHeight { get; private set; }

        /// <summary>
        /// The destination nearest the nose, within 30°: a planet's index in <see cref="BackdropBodies"/>,
        /// <see cref="HomeHeading"/> for the sector, or <see cref="NoHeading"/>.
        /// </summary>
        public int HeadingIndex { get; private set; } = NoHeading;

        /// <summary>Height above the heading destination's surface, or distance to the sector's edge, in metres.</summary>
        public double HeadingDistance { get; private set; }

        /// <summary>Where the autopilot is going while charging or cruising, else <see cref="NoHeading"/>.</summary>
        public int Destination { get; private set; } = NoHeading;

        /// <summary>The planet the autopilot is going round, or -1 on a straight course.</summary>
        public int Rounding { get; private set; } = -1;

        /// <summary>Angle between the nose and the autopilot's course, in degrees.</summary>
        public float CourseError { get; private set; }

        /// <summary>Why the drive will not engage on the destination ahead, or <see cref="CruiseBlock.None"/>.</summary>
        public CruiseBlock Block { get; private set; } = CruiseBlock.NoDestination;

        /// <summary>Distance along the route out of the sector to the first asteroid field, in metres. Infinite when clear.</summary>
        public float FieldDistance { get; private set; } = float.PositiveInfinity;

        /// <summary>True while the player is away from the sector, which sleeps.</summary>
        public bool IsAway => presence != null && presence.IsAsleep;

        /// <summary>The ship's place in the star system, in metres.</summary>
        public double3 SystemPosition => space != null ? space.WorldToSystem(body.position) : (double3)(float3)body.position;

        /// <summary>True when the drive can start charging now.</summary>
        public bool CanEngage => State == CruiseState.Ready && space != null && Block == CruiseBlock.None;

        /// <summary>The rules as plain values.</summary>
        public CruiseSettings Settings => new CruiseSettings
        {
            ApproachRate = approachRate,
            CrossRate = crossRate,
            MinSpeed = slowestSpeed,
            MaxSpeed = maxSpeed,
            SectorSpeed = sectorSpeed,
            Response = response,
            TurnRate = turnRate,
            TurnEase = turnEase
        };

        /// <summary>
        /// Locks the destination ahead and charges, cancels a charge, or drops out of cruise, as the drive's one control does.
        /// </summary>
        public void Toggle()
        {
            switch (State)
            {
                case CruiseState.Ready:
                    Resolve();
                    if (space == null)
                        return;

                    Survey(true);
                    if (Block != CruiseBlock.None)
                    {
                        Refused?.Invoke(Block);
                        return;
                    }

                    Destination = HeadingIndex;
                    CourseError = 180f;
                    State = CruiseState.Charging;
                    ChargeRemaining = chargeTime;
                    TakeControls();
                    ChargeStarted?.Invoke();
                    break;
                case CruiseState.Charging:
                    CancelCharge();
                    ChargeCancelled?.Invoke();
                    break;
                case CruiseState.Cruising:
                    DropOut(CruiseExit.Player);
                    break;
            }
        }

        /// <summary>
        /// Height above a planet's sea level where cruise to it ends, in metres: above the top of its air, which reaches past
        /// its visible sky (<see cref="AtmosphereFlight.AirHeightOf"/>).
        /// </summary>
        public double ArrivalHeightOf(int planet)
        {
            BackdropBodies backdrop = BackdropBodies.Active;
            double top = air != null ? air.AirHeightOf(planet) : backdrop != null ? backdrop.AtmosphereHeightOf(planet) : 0.0;
            return top > 0.0 ? top + arrivalMargin : airlessArrival;
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
            Resolve();
            if (space == null)
                return;

            if (State == CruiseState.Cruising)
                DropOut(CruiseExit.Player);
            else if (State == CruiseState.Charging)
                CancelCharge();

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
            Survey(true);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            air = GetComponent<AtmosphereFlight>();
            freeConstraints = body.constraints;
        }

        private void OnDisable()
        {
            if (State == CruiseState.Cruising)
                DropOut(CruiseExit.Interrupted);
            else if (State == CruiseState.Charging)
                CancelCharge();
        }

        private void Update()
        {
            Resolve();
            routeClock -= Time.deltaTime;
            bool checkRoute = routeClock <= 0f;
            if (checkRoute)
                routeClock = RouteInterval;

            Survey(checkRoute);
            switch (State)
            {
                case CruiseState.Charging:
                    KeepControls();
                    ChargeRemaining = Mathf.Max(0f, ChargeRemaining - Time.deltaTime);
                    if (ChargeRemaining <= 0f && CourseError <= alignTolerance)
                        Engage();
                    break;
                case CruiseState.Cruising:
                    KeepControls();
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

        // The autopilot steers at the physics rate, so the rigidbody's interpolation smooths the turn.
        private void FixedUpdate()
        {
            if (State == CruiseState.Ready || space == null)
                return;

            Vector3 heading = Steer(Time.fixedDeltaTime);
            if (State == CruiseState.Cruising && !holding)
                body.linearVelocity = heading * (float)Speed;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (State == CruiseState.Cruising)
                DropOut(CruiseExit.Collision);
        }

        private void Resolve()
        {
            if (space == null)
                space = SectorSpace.Active;
            if (presence == null)
                presence = SectorPresence.Active;
        }

        private void Engage()
        {
            // The ship may have drifted into a field's path while it charged.
            if (!IsAway && FieldOnRoute(Destination) < float.PositiveInfinity)
            {
                CancelCharge();
                Refused?.Invoke(CruiseBlock.Field);
                return;
            }

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
            double3 position = SystemPosition;
            double3 forward = (float3)transform.forward;
            double target = CruiseAutopilot.TargetSpeed(position, forward, bodies, Destination, SectorBounds(), settings);
            Speed = CruiseMath.Ease(Speed, target, settings.Response, deltaTime);

            if (!holding)
            {
                // Near the sector: the rigidbody moves. Past its reach, the sector sleeps and the ship holds still.
                if (!IsHome(position))
                    Depart();

                return;
            }

            double step = Speed * deltaTime;
            CruiseExit? exit = null;
            double toArrival = CruiseAutopilot.ToArrival(position, forward, bodies, Destination);
            if (toArrival <= step)
            {
                step = toArrival;
                exit = CruiseExit.Planet;
            }

            double toSurface = CruiseAutopilot.ToSurface(position, forward, bodies);
            if (toSurface <= step + SurfaceGap)
            {
                step = Math.Max(0.0, toSurface - SurfaceGap);
                exit = CruiseExit.Interrupted;
            }

            space.SetSystemOffset(space.SystemOffset + forward * step);
            if (exit.HasValue)
                DropOut(exit.Value);
            else if (Destination == HomeHeading && IsHome(position + forward * step))
                Arrive();
        }

        // Turns the ship towards the autopilot's course and returns the new heading.
        private Vector3 Steer(float deltaTime)
        {
            Quaternion rotation = body.rotation;
            Vector3 forward = rotation * Vector3.forward;
            double3 course = Course(out int around);
            Rounding = around;
            if (math.lengthsq(course) < 0.5)
                return forward;

            double3 nose = (float3)forward;
            CourseError = (float)math.degrees(CruiseMath.AngleBetween(nose, course));
            Vector3 turned = (float3)CruiseMath.Turn(nose, course, Settings, deltaTime);

            // The shortest turn keeps the ship's roll.
            Quaternion next = Quaternion.FromToRotation(forward, turned) * rotation;
            body.MoveRotation(next);
            body.angularVelocity = Vector3.zero;
            return turned;
        }

        private double3 Course(out int around)
        {
            double3 aim = AimOf(Destination);
            return CruiseAutopilot.Course(SystemPosition, aim, bodies, Destination, out around);
        }

        private double3 AimOf(int destination)
        {
            BackdropBodies backdrop = BackdropBodies.Active;
            return destination >= 0 && backdrop != null ? backdrop.CentreOf(destination) : GridCentre();
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
            body.linearVelocity = body.rotation * Vector3.forward * exitSpeed;
            Speed = 0.0;
            ReleaseControls();
            Dropped?.Invoke(exit);
            Destination = NoHeading;
            Rounding = -1;
        }

        private void CancelCharge()
        {
            State = CruiseState.Ready;
            ChargeRemaining = 0f;
            ReleaseControls();
            Destination = NoHeading;
            Rounding = -1;
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

        // The autopilot has the ship: the player's steering, throttle and boost are cleared and refused until it lets go.
        private void TakeControls()
        {
            if (engines == null)
                engines = GetComponentInChildren<Engines>();

            if (engines != null && !controlsTaken)
            {
                controlsBefore = engines.ControlsDisabled;
                throttleBefore = engines.MovementInputs;
                controlsTaken = true;
            }

            KeepControls();
        }

        // The kit's free look hands the engines back when it ends, so they are taken again every frame.
        private void KeepControls()
        {
            if (!controlsTaken || engines == null)
                return;

            engines.ClearAllInputs();
            engines.ControlsDisabled = true;
        }

        // The player gets their throttle back as they left it; arriving at a planet, the air's flight sets it full.
        private void ReleaseControls()
        {
            if (!controlsTaken)
                return;

            if (engines != null)
            {
                engines.ControlsDisabled = false;
                engines.SetMovementInputs(throttleBefore);
                engines.ControlsDisabled = controlsBefore;
            }

            controlsTaken = false;
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

        // Clearance, the nearest planet, the destination ahead and whether the drive may engage on it.
        private void Survey(bool checkRoute)
        {
            if (space == null)
                return;

            RefreshBodies();
            double3 position = SystemPosition;
            double clearance = DistanceToSector(position);
            int nearest = -1;

            int heading = NoHeading;
            double headingDistance = 0.0;
            float bestAngle = HeadingCone;
            if (IsAway)
                ConsiderHeading(GridCentre() - position, HomeHeading, clearance, ref heading, ref headingDistance, ref bestAngle);

            for (int i = 0; i < bodies.Count; ++i)
            {
                CruiseBody planet = bodies[i];
                if (!planet.Exists)
                    continue;

                double altitude = CruiseMath.Altitude(position, planet.Centre, planet.Radius);
                if (altitude < clearance)
                {
                    clearance = altitude;
                    nearest = i;
                }

                ConsiderHeading(planet.Centre - position, i, altitude, ref heading, ref headingDistance, ref bestAngle);
            }

            Clearance = Math.Max(0.0, clearance);
            NearestPlanet = nearest;
            NearestArrivalHeight = nearest >= 0 ? bodies[nearest].ArrivalHeight : 0.0;
            IsLow = nearest >= 0 && clearance < NearestArrivalHeight;
            HeadingIndex = heading;
            HeadingDistance = Math.Max(0.0, headingDistance);

            if (State != CruiseState.Ready)
                return;

            if (IsAway || heading == NoHeading)
                FieldDistance = float.PositiveInfinity;
            else if (checkRoute)
                FieldDistance = FieldOnRoute(heading);

            Block = BlockFor(heading, headingDistance, position);

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

        private CruiseBlock BlockFor(int heading, double headingDistance, double3 position)
        {
            if (heading == NoHeading)
                return CruiseBlock.NoDestination;

            bool there = heading == HomeHeading ? IsHome(position) : headingDistance < bodies[heading].ArrivalHeight;
            if (there)
                return CruiseBlock.AlreadyThere;

            return FieldDistance < float.PositiveInfinity ? CruiseBlock.Field : CruiseBlock.None;
        }

        // The run's planets as the autopilot sees them, in the order of the backdrop's plans.
        private void RefreshBodies()
        {
            bodies.Clear();
            BackdropBodies backdrop = BackdropBodies.Active;
            if (backdrop == null)
                return;

            for (int i = 0; i < backdrop.Plans.Count; ++i)
            {
                bodies.Add(backdrop.IsBuilt(i)
                    ? new CruiseBody { Centre = backdrop.CentreOf(i), Radius = backdrop.RadiusOf(i), ArrivalHeight = ArrivalHeightOf(i) }
                    : default);
            }
        }

        // The straight route from the ship towards the destination, through the sector's box, against its fields.
        private float FieldOnRoute(int destination)
        {
            SectorGrid grid = space.Grid;
            float3 from = space.WorldToSector(body.position);
            float3 to = (float3)(AimOf(destination) - space.SystemOffset);
            float3 min = grid.Min;
            return CruiseRoute.FieldAlong(space.Layout.Set, from, to, min, min + (float3)grid.Size, RouteSpacing, fieldLimit);
        }

        private CruiseSector SectorBounds()
        {
            SectorGrid grid = space.Grid;
            double3 min = (double3)(float3)grid.Min;
            return new CruiseSector { Min = min, Max = min + (double3)(float3)grid.Size, Reach = homeMargin };
        }

        private double DistanceToSector(double3 systemPosition)
        {
            CruiseSector sector = SectorBounds();
            return CruiseMath.DistanceToBox(systemPosition, sector.Min, sector.Max);
        }

        private double3 GridCentre()
        {
            SectorGrid grid = space.Grid;
            return (double3)(float3)(grid.Min + grid.Size * 0.5f);
        }
    }
}
