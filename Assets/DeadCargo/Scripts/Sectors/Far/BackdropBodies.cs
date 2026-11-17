using System;
using System.Collections.Generic;
using DeadCargo.Visuals;
using SpaceGraphicsToolkit.Sky;
using Unity.Mathematics;
using UnityEngine;

namespace DeadCargo.Sectors.Far
{
    /// <summary>
    /// The run's planets, two by default: a massive primary and a smaller planet elsewhere in the sky (moons are off unless
    /// their count is raised). Only the two chosen are loaded; the rest of the catalogue stays on disk. Chosen and placed
    /// in the sky from the sector seed (<see cref="BackdropLayout"/>), clear of the sun, and built in the far world at the
    /// distance that gives each its size. A new seed builds a new system; a pinned seed brings the same one back. Each body
    /// turns slowly about its own tilted axis. Play Mode only.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FarSpace))]
    [DefaultExecutionOrder(100)]
    public sealed class BackdropBodies : MonoBehaviour
    {
        // Separates the planets from every other use of the sector seed.
        private const uint SystemStream = 0x9A1E7u;

        // The sky shader's defaults, for a sky without the properties.
        private const float DefaultSunSize = 28f;
        private const float DefaultCoronaSize = 5f;

        // Far content beyond the furthest body's far side, so atmospheres are never cut off.
        private const float ReachMargin = 1.25f;

        private static readonly int SunSizeId = Shader.PropertyToID(SpaceSkyProperties.SunSize);
        private static readonly int CoronaSizeId = Shader.PropertyToID(SpaceSkyProperties.SunCoronaSize);

        [Tooltip("The planets a run is built from. Created by Add Planets in the God Menu Project tab.")]
        [SerializeField] private PlanetCatalog catalog;

        [Header("Primary")]
        [Tooltip("Chance a run has a primary planet, 0 to 1. Without one there are no moons.")]
        [SerializeField, Range(0f, 1f)] private float primaryChance = 1f;

        [Tooltip("Smallest primary, in degrees across. The sun is about 28 by default.")]
        [SerializeField, Range(1f, 90f)] private float primaryMinSize = 16f;

        [Tooltip("Largest primary, in degrees across.")]
        [SerializeField, Range(1f, 90f)] private float primaryMaxSize = 34f;

        [Tooltip("Least angle between the primary's centre and the sun's, in degrees. Low shows the primary's night side, a thin " +
                 "crescent; 90 shows half of it lit.")]
        [SerializeField, Range(0f, 180f)] private float primaryMinFromSun = 60f;

        [Tooltip("Greatest angle between the primary's centre and the sun's, in degrees. 180 is straight across the sky, " +
                 "fully lit.")]
        [SerializeField, Range(0f, 180f)] private float primaryMaxFromSun = 160f;

        [Tooltip("Least gap between where the player first looks and the primary's edge, in degrees. 0 lets it sit anywhere, " +
                 "in the opening view included.")]
        [SerializeField, Range(0f, 180f)] private float startViewClearance;

        [Header("Moons")]
        [Tooltip("Fewest moons beside the primary.")]
        [SerializeField, Range(0, BackdropLayout.MaxPerKind)] private int minMoons;

        [Tooltip("Most moons beside the primary. 0 by default: a run has two planets, the primary and one distant planet.")]
        [SerializeField, Range(0, BackdropLayout.MaxPerKind)] private int maxMoons;

        [Tooltip("Smallest moon, in degrees across.")]
        [SerializeField, Range(0.2f, 30f)] private float moonMinSize = 2f;

        [Tooltip("Largest moon, in degrees across.")]
        [SerializeField, Range(0.2f, 30f)] private float moonMaxSize = 6f;

        [Tooltip("Least angle from the primary's edge to a moon's centre, in degrees.")]
        [SerializeField, Range(0f, 90f)] private float moonMinGap = 2f;

        [Tooltip("Greatest angle from the primary's edge to a moon's centre, in degrees.")]
        [SerializeField, Range(0f, 90f)] private float moonMaxGap = 16f;

        [Header("Distant Planets")]
        [Tooltip("Fewest smaller planets elsewhere in the sky, besides the primary.")]
        [SerializeField, Range(0, BackdropLayout.MaxPerKind)] private int minDistant = 1;

        [Tooltip("Most smaller planets elsewhere in the sky, besides the primary.")]
        [SerializeField, Range(0, BackdropLayout.MaxPerKind)] private int maxDistant = 1;

        [Tooltip("Smallest distant planet, in degrees across.")]
        [SerializeField, Range(0.2f, 30f)] private float distantMinSize = 3f;

        [Tooltip("Largest distant planet, in degrees across.")]
        [SerializeField, Range(0.2f, 30f)] private float distantMaxSize = 8f;

        [Header("Placement")]
        [Tooltip("Highest the primary and distant planets sit above or below the sector's plane, in degrees.")]
        [SerializeField, Range(0f, 90f)] private float maxElevation = 40f;

        [Tooltip("Least gap between the sun's edge, corona included, and any body's edge, in degrees.")]
        [SerializeField, Range(0f, 60f)] private float sunClearance = 6f;

        [Tooltip("Least gap between the edges of any two bodies, in degrees.")]
        [SerializeField, Range(0f, 30f)] private float bodyClearance = 2f;

        [Header("Spin")]
        [Tooltip("Greatest tilt of a body's spin axis from the sector's up, in degrees.")]
        [SerializeField, Range(0f, 90f)] private float maxTilt = 25f;

        [Tooltip("Slowest a body turns, in degrees a second.")]
        [SerializeField, Range(0f, 2f)] private float minSpin = 0.02f;

        [Tooltip("Fastest a body turns, in degrees a second. 0.1 turns a full circle in an hour, enough to see clouds and " +
                 "coasts drift over a minute.")]
        [SerializeField, Range(0f, 2f)] private float maxSpin = 0.12f;

        private readonly List<BackdropPlan> plans = new List<BackdropPlan>();
        private readonly List<Body> bodies = new List<Body>();
        private readonly List<Transform> instances = new List<Transform>();
        private readonly List<double> airHeights = new List<double>();

        private FarSpace far;
        private SectorSpace space;
        private Vector3 startView = Vector3.forward;
        private bool started;
        private bool rebuild;
        private bool warned;
        private float spinClock;
        private bool spinHeld;

        /// <summary>The planets this component builds from.</summary>
        public PlanetCatalog Catalog => catalog;

        /// <summary>This run's bodies, primary first.</summary>
        public IReadOnlyList<BackdropPlan> Plans => plans;

        /// <summary>The sun's angular radius, corona included, in degrees, when the system was last built.</summary>
        public float SunRadius { get; private set; }

        /// <summary>The rules as plain values.</summary>
        public BackdropSettings Settings => new BackdropSettings
        {
            PrimaryChance = primaryChance,
            PrimaryMinSize = primaryMinSize,
            PrimaryMaxSize = primaryMaxSize,
            PrimaryMinFromSun = primaryMinFromSun,
            PrimaryMaxFromSun = primaryMaxFromSun,
            MinMoons = minMoons,
            MaxMoons = maxMoons,
            MoonMinSize = moonMinSize,
            MoonMaxSize = moonMaxSize,
            MoonMinGap = moonMinGap,
            MoonMaxGap = moonMaxGap,
            MinDistant = minDistant,
            MaxDistant = maxDistant,
            DistantMinSize = distantMinSize,
            DistantMaxSize = distantMaxSize,
            MaxElevation = maxElevation,
            SunClearance = sunClearance,
            BodyClearance = bodyClearance,
            StartViewClearance = startViewClearance,
            MaxTilt = maxTilt,
            MinSpin = minSpin,
            MaxSpin = maxSpin
        };

        /// <summary>The backdrop bodies of the loaded scene, or null.</summary>
        public static BackdropBodies Active { get; private set; }

        /// <summary>Raised after the system is built or rebuilt.</summary>
        public event Action Built;

        /// <summary>How many times the system has been built, so readers can tell when it changed.</summary>
        public int BuildCount { get; private set; }

        /// <summary>The name of the planet playing body <paramref name="index"/>.</summary>
        public string NameOf(int index)
        {
            PlanetEntry entry = EntryOf(index);
            return entry != null ? entry.Name : string.Empty;
        }

        /// <summary>True when body <paramref name="index"/> was built, so it stands in the sky.</summary>
        public bool IsBuilt(int index)
        {
            return index >= 0 && index < instances.Count && instances[index] != null;
        }

        /// <summary>The body's centre in the star system, in metres.</summary>
        public double3 CentreOf(int index)
        {
            PlanetEntry entry = EntryOf(index);
            if (entry == null || far == null)
                return double3.zero;

            BackdropPlan plan = plans[index];
            return (double3)(float3)plan.Direction * ((double)plan.DistanceFor(entry.Radius) * far.Scale);
        }

        /// <summary>The body's radius at the surface, in metres.</summary>
        public double RadiusOf(int index)
        {
            PlanetEntry entry = EntryOf(index);
            return entry != null && far != null ? (double)entry.Radius * far.Scale : 0.0;
        }

        /// <summary>Height of the body's atmosphere above its surface, in metres. 0 for a body with no air.</summary>
        public double AtmosphereHeightOf(int index)
        {
            return IsBuilt(index) && index < airHeights.Count ? airHeights[index] : 0.0;
        }

        /// <summary>The body's copy in the far world, for screen markers. Null when it was not built.</summary>
        public Transform InstanceOf(int index)
        {
            return IsBuilt(index) ? instances[index] : null;
        }

        /// <summary>Holds every body still, or lets them turn again. A planet's spin would slide its surface under a ship near it.</summary>
        public void SetSpinHeld(bool held)
        {
            spinHeld = held;
        }

        /// <summary>The catalogue entry of the planet playing body <paramref name="index"/>, or null.</summary>
        public PlanetEntry EntryOf(int index)
        {
            if (catalog == null || index < 0 || index >= plans.Count)
                return null;

            int entry = plans[index].Entry;
            return entry >= 0 && entry < catalog.Count ? catalog.Entries[entry] : null;
        }

        private void OnEnable()
        {
            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
        }

        /// <summary>Builds the system again from the current seed and sun. For debug tools.</summary>
        public void Rebuild()
        {
            if (started)
                rebuild = true;
        }

        // Start, not Awake: the sector space has drawn the run's seed, Space Lighting (order 0) has aimed the sun, and the
        // camera stands where the player first looks.
        private void Start()
        {
            started = true;
            far = GetComponent<FarSpace>();
            space = SectorSpace.Active;
            if (space != null)
                space.SeedChanged += Rebuild;

            Camera view = Camera.main;
            if (view != null)
                startView = view.transform.forward;

            Build();
        }

        private void OnDestroy()
        {
            if (space != null)
                space.SeedChanged -= Rebuild;
        }

        // Rules changed in Play Mode show at once.
        private void OnValidate()
        {
            Rebuild();
        }

        // A rebuild waits for Update, after Space Lighting has turned the sun for a new seed.
        private void Update()
        {
            if (rebuild)
            {
                rebuild = false;
                Build();
            }

            Spin();
        }

        private void Build()
        {
            Clear();
            if (far == null || !far.IsValid || catalog == null || catalog.Count == 0)
            {
                if (!warned)
                    Debug.LogWarning("Backdrop bodies need the far space's layer and a planet catalogue. Press Add Planets in the God Menu Project tab.", this);

                warned = true;
                return;
            }

            uint seed = space != null
                ? SectorHash.Combine(space.Seed, SystemStream)
                : SectorHash.Mix(unchecked((uint)DateTime.UtcNow.Ticks ^ (uint)Environment.TickCount));

            SunRadius = SunAngularRadius();
            BackdropLayout.Plan(seed, Settings, SceneSun.Direction, SunRadius, startView, catalog.Entries, plans);

            float furthest = 0f;
            Vector3 origin = far.World.position;
            for (int i = 0; i < plans.Count; ++i)
            {
                BackdropPlan plan = plans[i];
                PlanetEntry entry = catalog.Entries[plan.Entry];
                GameObject prefab = PlanetCatalog.Load(entry);
                instances.Add(null);
                airHeights.Add(0.0);
                if (prefab == null)
                    continue;

                // Sector directions and the far world share their axes, so a direction places the body as it is.
                float distance = plan.DistanceFor(entry.Radius);
                Quaternion tilt = Quaternion.FromToRotation(Vector3.up, plan.SpinAxis);
                GameObject instance = Instantiate(prefab, origin + plan.Direction * distance, tilt);
                instance.name = entry.Name;
                far.Adopt(instance);

                instances[i] = instance.transform;
                airHeights[i] = AtmosphereHeight(instance, entry.Radius);
                bodies.Add(new Body(instance.transform, tilt, plan.SpinPhase, plan.SpinSpeed));
                furthest = Mathf.Max(furthest, distance + entry.Radius * ReachMargin);
            }

            // Cruise can carry the camera to the far side of the system, so the far camera reaches across all of it.
            far.SetReach(furthest * 2f);
            spinClock = 0f;
            Spin();
            ++BuildCount;
            Built?.Invoke();
        }

        private void Clear()
        {
            foreach (Body body in bodies)
            {
                if (body.Transform != null)
                    Destroy(body.Transform.gameObject);
            }

            bodies.Clear();
            instances.Clear();
            airHeights.Clear();
            plans.Clear();
        }

        // The top of the planet's sky above its surface, in true metres.
        private double AtmosphereHeight(GameObject instance, float radius)
        {
            SgtSky sky = instance.GetComponentInChildren<SgtSky>(true);
            if (sky == null)
                return 0.0;

            double top = (sky.InnerMeshRadius + sky.Height) * (double)sky.transform.lossyScale.x;
            return Math.Max(0.0, (top - radius) * far.Scale);
        }

        // Each body's turn comes from a clock that runs from the build, so it never drifts, and stops while spin is held.
        private void Spin()
        {
            if (!spinHeld)
                spinClock += Time.deltaTime;

            float elapsed = spinClock;
            foreach (Body body in bodies)
            {
                if (body.Transform != null)
                    body.Transform.rotation = body.Tilt * Quaternion.AngleAxis(body.Phase + body.Speed * elapsed, Vector3.up);
            }
        }

        // The sun's radius in the sky, corona included, as the run's sky draws it.
        private static float SunAngularRadius()
        {
            Material sky = RenderSettings.skybox;
            bool hasSun = sky != null && sky.HasProperty(SunSizeId);
            float size = hasSun ? sky.GetFloat(SunSizeId) : DefaultSunSize;
            float corona = hasSun && sky.HasProperty(CoronaSizeId) ? sky.GetFloat(CoronaSizeId) : DefaultCoronaSize;
            return size * 0.5f + corona;
        }

        private readonly struct Body
        {
            public Body(Transform transform, Quaternion tilt, float phase, float speed)
            {
                Transform = transform;
                Tilt = tilt;
                Phase = phase;
                Speed = speed;
            }

            public Transform Transform { get; }
            public Quaternion Tilt { get; }
            public float Phase { get; }
            public float Speed { get; }
        }
    }
}
