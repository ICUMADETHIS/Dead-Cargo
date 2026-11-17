using System;
using DeadCargo.Sectors;
using DeadCargo.Sectors.Far;
using Unity.Mathematics;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Flight
{
    /// <summary>
    /// How the player's ship flies near a planet, out of cruise.
    /// <para>
    /// <b>The air.</b> A planet's air reaches higher than its visible sky (<c>airReach</c>), thin and almost clear at the
    /// top. The thinner it is, the faster the ship flies (<see cref="PlanetPhysics.SpeedFactor"/>): 25 times its open-space
    /// speed at the top of the air, falling to 1.5 times at the ground, so a descent starts fast and slows as the ground
    /// comes up. Thrust rises and drag falls by the square root of that factor each, so the ship keeps its feel, only
    /// heavier at speed. The kit's engines are untouched: the extra thrust is added beside theirs, from the same inputs.
    /// </para>
    /// <para>
    /// <b>Gravity.</b> Within the air and its <c>range</c> above it, the ship is pulled towards the planet's centre, weaker
    /// with the square of the distance.
    /// </para>
    /// <para>
    /// <b>Arriving.</b> When cruise ends at a planet, above its air and nose down, the ship keeps the speed the air allows
    /// there with the throttle full, so the dive carries straight on.
    /// </para>
    /// <para>
    /// <b>Heating.</b> Fast through thick enough air, the hull heats (<see cref="Glow"/>), which <see cref="EntryGlow"/> shows.
    /// </para>
    /// Lives on the player's ship, beside its cruise drive.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AtmosphereFlight : MonoBehaviour
    {
        [Header("Air")]
        [Tooltip("How high a planet's air reaches, as a multiple of its visible sky. Above the sky the air is thin and " +
                 "almost clear, so a descent starts in it, fast. Cruise ends just above it.")]
        [SerializeField, Range(1f, 5f)] private float airReach = 2f;

        [Tooltip("Top speed at the ground, as a multiple of the ship's top speed in open space.")]
        [SerializeField, Range(0.5f, 5f)] private float groundFactor = 1.5f;

        [Tooltip("Top speed at the top of the air and above it, as a multiple of the ship's top speed in open space. With " +
                 "the fighter's 133 m/s, 25 is 3.3 km/s.")]
        [SerializeField, Range(1f, 60f)] private float topFactor = 25f;

        [Tooltip("How the speed falls as the air thickens: 1 evenly with height, higher keeps the ship fast for longer.")]
        [SerializeField, Range(0.25f, 4f)] private float curve = 1f;

        [Tooltip("How far above the top of the air the planet's pull and the fast flight reach, in metres. The speed eases " +
                 "back to open space over the outer half.")]
        [SerializeField, Min(1000f)] private float range = 40000f;

        [Header("Gravity")]
        [Tooltip("Pull towards a planet's centre at its surface, in metres per second squared. Weaker with the square of " +
                 "the distance. The kit's ships fly against heavy drag, so near the ground this is a gentle sink.")]
        [SerializeField, Min(0f)] private float surfaceGravity = 6f;

        [Header("Heating")]
        [Tooltip("Heating where the entry glow starts: air density (1 at sea level) times speed in kilometres a second, cubed.")]
        [SerializeField, Min(0f)] private float glowStart = 0.06f;

        [Tooltip("Heating where the entry glow is full.")]
        [SerializeField, Min(0.001f)] private float glowFull = 0.13f;

        private Rigidbody body;
        private VehicleEngines3D engines;
        private CruiseDrive drive;
        private float baseDamping;
        private Vector3 down;

        /// <summary>The planet whose air and pull the ship is in, or -1.</summary>
        public int Planet { get; private set; } = -1;

        /// <summary>Height above the nearest planet's sea level, in metres. Infinite with no planet near.</summary>
        public double Altitude { get; private set; } = double.PositiveInfinity;

        /// <summary>How high the nearest planet's air reaches above its sea level, in metres. 0 with no air.</summary>
        public double AirHeight { get; private set; }

        /// <summary>The ship's top speed now, as a multiple of its top speed in open space.</summary>
        public double SpeedFactor { get; private set; } = 1.0;

        /// <summary>The ship's top speed at full throttle now, in metres a second.</summary>
        public double TopSpeed { get; private set; }

        /// <summary>The pull on the ship now, in metres per second squared.</summary>
        public double Gravity { get; private set; }

        /// <summary>The air's density here, 1 at sea level.</summary>
        public double Density { get; private set; }

        /// <summary>The ship's speed through the air, in metres a second, cruise included.</summary>
        public double AirSpeed { get; private set; }

        /// <summary>Heating on the hull: density times speed in kilometres a second, cubed.</summary>
        public double Heating { get; private set; }

        /// <summary>How strongly the hull glows from heating, 0 to 1.</summary>
        public float Glow { get; private set; }

        /// <summary>
        /// Which way is up for level flight, in world space: away from the planet's centre within its reach, the sector's up
        /// in open space, turning from one to the other over the outer half of the range.
        /// </summary>
        public Vector3 Up { get; private set; } = Vector3.up;

        /// <summary>The direction the ship moves through the air, in world space.</summary>
        public Vector3 Travel { get; private set; } = Vector3.forward;

        /// <summary>The rules as plain values.</summary>
        public AirSettings Settings => new AirSettings
        {
            GroundFactor = groundFactor,
            TopFactor = topFactor,
            Curve = curve,
            Range = range,
            GlowStart = glowStart,
            GlowFull = glowFull
        };

        /// <summary>How high a planet's air reaches above its sea level, in metres. 0 for a planet with no air.</summary>
        public double AirHeightOf(int planet)
        {
            BackdropBodies backdrop = BackdropBodies.Active;
            return backdrop != null ? backdrop.AtmosphereHeightOf(planet) * airReach : 0.0;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            engines = GetComponent<VehicleEngines3D>();
            drive = GetComponent<CruiseDrive>();
            baseDamping = body.linearDamping;
        }

        private void OnEnable()
        {
            if (drive != null)
                drive.Dropped += OnCruiseDropped;
        }

        private void OnDisable()
        {
            if (drive != null)
                drive.Dropped -= OnCruiseDropped;

            if (body != null && (drive == null || drive.State != CruiseState.Cruising))
                body.linearDamping = baseDamping;
        }

        private void FixedUpdate()
        {
            Survey();

            // In cruise the drive flies the ship; its speed still heats the hull.
            bool cruising = drive != null && drive.State == CruiseState.Cruising;
            AirSpeed = cruising ? drive.Speed : body.linearVelocity.magnitude;
            Travel = cruising || AirSpeed < 1.0 ? transform.forward : body.linearVelocity / (float)AirSpeed;
            Heating = PlanetPhysics.Heating(Density, AirSpeed);
            Glow = (float)PlanetPhysics.Glow(Heating, Settings);
            if (cruising || body.isKinematic)
                return;

            double scale = Math.Sqrt(SpeedFactor);
            body.linearDamping = (float)(baseDamping / scale);
            if (scale > 1.0 + 1e-6)
                body.AddRelativeForce(EngineForce() * (float)(scale - 1.0));

            if (Gravity > 0.0)
                body.AddForce(down * (float)Gravity, ForceMode.Acceleration);
        }

        // The nearest planet, and its air and pull here.
        private void Survey()
        {
            Planet = -1;
            Altitude = double.PositiveInfinity;
            AirHeight = 0.0;
            SpeedFactor = 1.0;
            Gravity = 0.0;
            Density = 0.0;
            Up = Vector3.up;

            SectorSpace space = SectorSpace.Active;
            BackdropBodies backdrop = BackdropBodies.Active;
            if (space != null && backdrop != null)
            {
                double3 position = space.WorldToSystem(body.position);
                int nearest = -1;
                double altitude = double.PositiveInfinity;
                for (int i = 0; i < backdrop.Plans.Count; ++i)
                {
                    if (!backdrop.IsBuilt(i))
                        continue;

                    double height = CruiseMath.Altitude(position, backdrop.CentreOf(i), backdrop.RadiusOf(i));
                    if (height < altitude)
                    {
                        altitude = height;
                        nearest = i;
                    }
                }

                if (nearest >= 0)
                {
                    Altitude = altitude;
                    double air = AirHeightOf(nearest);
                    if (altitude < air + range)
                    {
                        AirSettings settings = Settings;
                        Planet = nearest;
                        AirHeight = air;
                        SpeedFactor = PlanetPhysics.SpeedFactor(altitude, air, settings);
                        Density = PlanetPhysics.AirDensity(altitude, air);

                        double3 toCentre = backdrop.CentreOf(nearest) - position;
                        double distance = math.length(toCentre);
                        Gravity = PlanetPhysics.GravityAt(distance, backdrop.RadiusOf(nearest), surfaceGravity);
                        down = distance > 0.0 ? (Vector3)(float3)(toCentre / distance) : Vector3.zero;
                        Up = Vector3.Slerp(Vector3.up, -down, (float)PlanetPhysics.Reach(altitude, air, settings));
                    }
                }
            }

            TopSpeed = OpenSpaceTopSpeed() * SpeedFactor;
        }

        // The force the kit's engines push with now, from the same inputs and in the same way, before their smoothing.
        private Vector3 EngineForce()
        {
            if (engines == null || !engines.EnginesActivated)
                return Vector3.zero;

            Vector3 force = Vector3.Scale(engines.MovementInputs, engines.MaxMovementForces);
            Vector3 boost = engines.BoostInputs;
            Vector3 boostForces = engines.MaxBoostForces;
            if (boost.x > 0.5f)
                force.x = boostForces.x;
            if (boost.y > 0.5f)
                force.y = boostForces.y;
            if (boost.z > 0.5f)
                force.z = boostForces.z;

            return force;
        }

        // Full throttle against the kit's drag, as the kit works it out.
        private double OpenSpaceTopSpeed()
        {
            if (engines == null || body.mass <= 0f || baseDamping <= 0f)
                return 0.0;

            return engines.MaxMovementForces.z / body.mass / baseDamping;
        }

        // Cruise ends above the air, nose down: the dive carries on at the speed the air allows, throttle full.
        private void OnCruiseDropped(CruiseExit exit)
        {
            if (exit != CruiseExit.Planet || engines == null)
                return;

            Survey();
            body.linearVelocity = body.rotation * Vector3.forward * (float)TopSpeed;
            engines.SetMovementInputs(new Vector3(0f, 0f, 1f));
        }
    }
}
