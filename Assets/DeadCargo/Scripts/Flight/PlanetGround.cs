using DeadCargo.Sectors;
using DeadCargo.Sectors.Far;
using SpaceGraphicsToolkit.Cloud;
using SpaceGraphicsToolkit.Landscape;
using SpaceGraphicsToolkit.Ocean;
using SpaceGraphicsToolkit.Sky;
using Unity.Mathematics;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Flight
{
    /// <summary>
    /// The ground of the planet the player is near: something to touch and land on.
    /// <para>
    /// <b>One picture.</b> The planet is drawn only by its copy in the far world, at every height, so nothing changes on
    /// screen as the ship comes down. Physics needs the planet at true scale in the near world, so below
    /// <c>groundRange</c> and out of cruise an invisible <b>ground copy</b> is built around the ship: the same prefab without
    /// its atmosphere, clouds or sea, scaled up by the far world's scale, on a layer no camera draws, with Planet Forge's
    /// landscape collider. Both copies see the camera from the same place in their own scale, so Planet Forge builds the
    /// same triangles for both and the colliders match what is seen. A sphere at sea level makes the sea solid.
    /// </para>
    /// <para>
    /// While a ground copy exists, both copies build triangles down to <c>groundDetail</c> and the far camera draws from
    /// <c>groundNearClip</c>. While the player is away from the sector every body holds still, as a turning planet would
    /// slide its surface under the ship. The planet's pull is the ship's own (<see cref="AtmosphereFlight"/>).
    /// </para>
    /// Lives on the Far Space object, beside the planets it grounds. Play Mode only.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FarSpace), typeof(BackdropBodies))]
    public sealed class PlanetGround : MonoBehaviour
    {
        /// <summary>The name of the layer the ground copy sits on.</summary>
        public const string LayerName = "Planet Ground";

        // The ground copy goes this much higher than it came, so it is not rebuilt over and over at the edge.
        private const float LeaveFactor = 1.25f;

        // A move of the ground copy smaller than this, in metres, is not made, so it is not shifted every frame for nothing.
        private const float MoveTolerance = 0.01f;

        // Altitude is measured from this far above the ship towards the ground, in metres, so the ray starts outside it.
        private const float RayLift = 0.5f;

        // Room for every surface a ray down may cross: the ship's own colliders, other objects and the ground.
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        [Tooltip("The layer the ground copy sits on, which no camera draws. Set by Set Up Sector.")]
        [SerializeField] private int layer = -1;

        [Tooltip("Height above a planet's sea level below which its ground is solid, in metres. Above it, the ground copy is " +
                 "removed.")]
        [SerializeField, Min(1000f)] private float groundRange = 40000f;

        [Tooltip("Smallest terrain triangle on the planet you are near, in metres: the detail you fly over and land on, and " +
                 "of its colliders. Smaller is finer and costs more.")]
        [SerializeField, Range(0.25f, 50f)] private float groundDetail = 2f;

        [Tooltip("Nearest the far camera draws while near a planet's ground, in metres, so ground close below the ship is " +
                 "not cut away.")]
        [SerializeField, Range(0.05f, 100f)] private float groundNearClip = 0.5f;

        private FarSpace far;
        private BackdropBodies bodies;
        private SectorPresence presence;

        private GameObject ground;
        private Rigidbody groundBody;
        private SgtSphereLandscape farLandscape;
        private float farDetailBefore;

        private Rigidbody ship;
        private CruiseDrive drive;

        /// <summary>The ground of the loaded scene, or null.</summary>
        public static PlanetGround Active { get; private set; }

        /// <summary>The body whose ground copy exists, or -1.</summary>
        public int Planet { get; private set; } = -1;

        /// <summary>True while a planet's ground is solid under the ship.</summary>
        public bool HasGround => ground != null;

        /// <summary>The ship's height above the nearest planet's sea level, in metres. Infinite with no planet near.</summary>
        public double Altitude { get; private set; } = double.PositiveInfinity;

        /// <summary>The ship's height above the ground below it, in metres: by a ray to the ground copy, else above sea level.</summary>
        public double GroundDistance { get; private set; } = double.PositiveInfinity;

        /// <summary>The layer the ground copy sits on.</summary>
        public int Layer => layer;

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
        }

        private void Awake()
        {
            far = GetComponent<FarSpace>();
            bodies = GetComponent<BackdropBodies>();
        }

        private void OnEnable()
        {
            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this)
                Active = null;

            RemoveGround();
        }

        private void Start()
        {
            if (layer > 0 && layer < 32)
                far.HideLayer(layer);
            else
                Debug.LogWarning("Planet Ground has no layer of its own, so planets have no ground. Run Set Up Sector.", this);
        }

        private void LateUpdate()
        {
            SectorSpace space = SectorSpace.Active;
            if (presence == null)
                presence = SectorPresence.Active;

            bool away = presence != null && presence.IsAsleep;
            bodies.SetSpinHeld(away);
            FindShip();
            if (space == null || ship == null || layer <= 0)
            {
                RemoveGround();
                return;
            }

            double3 position = space.WorldToSystem(ship.position);
            int nearest = NearestPlanet(position, out double altitude);
            Altitude = altitude;

            bool cruising = drive != null && drive.State == CruiseState.Cruising;
            float range = HasGround ? groundRange * LeaveFactor : groundRange;
            bool wanted = away && !cruising && nearest >= 0 && altitude < range;

            if (HasGround && (!wanted || nearest != Planet))
                RemoveGround();
            if (wanted && !HasGround)
                BuildGround(nearest);

            if (HasGround)
                PlaceGround(space);

            far.SetNearClipOverride(HasGround ? groundNearClip / far.Scale : 0f);
            GroundDistance = HasGround ? MeasureGround(space) : altitude;
        }

        private void FindShip()
        {
            GameAgentManager agents = GameAgentManager.Instance;
            GameAgent player = agents != null ? agents.FocusedGameAgent : null;
            Vehicle vehicle = player != null ? player.Vehicle : null;
            Rigidbody body = vehicle != null ? vehicle.GetComponent<Rigidbody>() : null;
            if (body == ship)
                return;

            ship = body;
            drive = body != null ? body.GetComponent<CruiseDrive>() : null;
        }

        private int NearestPlanet(double3 position, out double altitude)
        {
            int nearest = -1;
            altitude = double.PositiveInfinity;
            for (int i = 0; i < bodies.Plans.Count; ++i)
            {
                if (!bodies.IsBuilt(i))
                    continue;

                double height = CruiseMath.Altitude(position, bodies.CentreOf(i), bodies.RadiusOf(i));
                if (height < altitude)
                {
                    altitude = height;
                    nearest = i;
                }
            }

            return nearest;
        }

        // Built inactive, so nothing it carries wakes before it is stripped to its landscape.
        private void BuildGround(int planet)
        {
            GameObject prefab = PlanetCatalog.Load(bodies.EntryOf(planet));
            Transform farCopy = bodies.InstanceOf(planet);
            if (prefab == null || farCopy == null)
                return;

            var holder = new GameObject("Ground Holder");
            holder.SetActive(false);
            GameObject copy = Instantiate(prefab, holder.transform);
            copy.name = $"{bodies.NameOf(planet)} (Ground)";

            SgtOcean sea = copy.GetComponentInChildren<SgtOcean>(true);
            float seaLevel = sea != null ? sea.Radius : 0f;
            StripToLandscape(copy);

            SgtSphereLandscape landscape = copy.GetComponentInChildren<SgtSphereLandscape>(true);
            if (landscape == null)
            {
                Destroy(holder);
                return;
            }

            float detail = groundDetail / far.Scale;
            landscape.MinimumTriangleSize = detail;
            var colliders = landscape.gameObject.AddComponent<SgtLandscapeCollider>();
            colliders.MinimumTriangleSize = detail;

            if (seaLevel > 0f)
            {
                var surface = new GameObject("Sea Surface").AddComponent<SphereCollider>();
                surface.transform.SetParent(copy.transform, false);
                surface.radius = seaLevel;
            }

            SetLayer(copy.transform, layer);
            groundBody = copy.AddComponent<Rigidbody>();
            groundBody.isKinematic = true;
            groundBody.interpolation = RigidbodyInterpolation.None;

            copy.transform.SetParent(transform, false);
            copy.transform.localScale = Vector3.one * far.Scale;
            copy.transform.rotation = farCopy.rotation;
            Destroy(holder);

            // The far copy builds the same triangles, so what is seen matches what is touched.
            farLandscape = farCopy.GetComponentInChildren<SgtSphereLandscape>(true);
            if (farLandscape != null)
            {
                farDetailBefore = farLandscape.MinimumTriangleSize;
                farLandscape.MinimumTriangleSize = detail;
            }

            ground = copy;
            Planet = planet;
            PlaceGround(SectorSpace.Active);
        }

        private void RemoveGround()
        {
            if (farLandscape != null)
                farLandscape.MinimumTriangleSize = farDetailBefore;

            if (ground != null)
                Destroy(ground);

            if (far != null)
                far.SetNearClipOverride(0f);

            ground = null;
            groundBody = null;
            farLandscape = null;
            Planet = -1;
        }

        // The ground copy stands where the planet is in the star system, so it moves only when the world's origin does.
        private void PlaceGround(SectorSpace space)
        {
            Vector3 target = space.SystemToWorld(bodies.CentreOf(Planet));
            Transform copy = ground.transform;
            if ((copy.position - target).sqrMagnitude < MoveTolerance * MoveTolerance)
                return;

            groundBody.position = target;
            copy.position = target;
            Transform farCopy = bodies.InstanceOf(Planet);
            if (farCopy != null)
                copy.rotation = farCopy.rotation;
        }

        // A ray from just above the ship towards the planet's centre, against the ground copy's colliders; above sea level
        // until they are ready.
        private double MeasureGround(SectorSpace space)
        {
            Vector3 down = (Vector3)(float3)math.normalize(bodies.CentreOf(Planet) - space.WorldToSystem(ship.position));
            Vector3 start = ship.position - down * RayLift;
            double reach = Altitude + groundRange;
            int count = Physics.RaycastNonAlloc(start, down, Hits, (float)reach, ~0, QueryTriggerInteraction.Ignore);
            double nearest = Altitude;
            for (int i = 0; i < count; ++i)
            {
                RaycastHit hit = Hits[i];
                if (hit.rigidbody == groundBody && hit.distance - RayLift < nearest)
                    nearest = hit.distance - RayLift;
            }

            return System.Math.Max(0.0, nearest);
        }

        // Atmosphere, clouds and sea are drawn by the far copy; the ground copy keeps only its landscape.
        private static void StripToLandscape(GameObject copy)
        {
            Strip(copy, copy.GetComponentsInChildren<SgtSky>(true));
            Strip(copy, copy.GetComponentsInChildren<SgtCloud>(true));
            Strip(copy, copy.GetComponentsInChildren<SgtOcean>(true));
        }

        // Removes each part's object, or only the part where its object is the root or also holds the landscape.
        private static void Strip(GameObject root, Component[] parts)
        {
            foreach (Component part in parts)
            {
                if (part == null)
                    continue;

                bool keepObject = part.gameObject == root || part.GetComponentInChildren<SgtSphereLandscape>(true) != null;
                if (keepObject)
                    DestroyImmediate(part);
                else
                    DestroyImmediate(part.gameObject);
            }
        }

        private static void SetLayer(Transform node, int newLayer)
        {
            node.gameObject.layer = newLayer;
            for (int i = 0; i < node.childCount; ++i)
                SetLayer(node.GetChild(i), newLayer);
        }
    }
}
