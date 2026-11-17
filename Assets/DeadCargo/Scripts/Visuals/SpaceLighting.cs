using System;
using DeadCargo.Sectors;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// The light of space, on the sun light: where the sun sits each run, and how dark space is.
    /// <para>
    /// <b>A new sun each run.</b> Each load turns the light to a new direction drawn from the sector's seed, so a pinned
    /// seed brings the same sun back. The sun keeps out of where the player first looks, so it is not on top of them at
    /// the start, but not behind them, where it would light only the side of everything they look at: from the side,
    /// ships and rocks show a lit half and a dark half. It stays within a band round the sector's plane, so it lights
    /// the belt from the side. The sky draws the star where the light comes from, so it moves with it.
    /// </para>
    /// <para>
    /// <b>Darkness.</b> Space has no air to scatter light: what the sun strikes is bright and the rest falls towards
    /// black. Shadowed sides of comic surfaces, their rims, the sky (never the sun) and the ambient light are darkened
    /// together, through a global that <c>ComicSurface.shader</c> and <c>SpaceSky.shader</c> read; without this
    /// component they are unchanged.
    /// </para>
    /// Play Mode only, so the editor shows the light and the scene as authored.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class SpaceLighting : MonoBehaviour
    {
        // The global both shaders read. x: shadows, y: sky, z: rims.
        private static readonly int ShadeId = Shader.PropertyToID("_SpaceShade");

        // Separates the sun from every other use of the sector seed.
        private const uint SunStream = 0x5D1A7u;

        // Directions tried before the one furthest from the start view is used.
        private const int Attempts = 64;

        [Header("Sun Direction")]
        [Tooltip("Turn the sun to a new direction every load, from the sector's seed. Off keeps the light as placed.")]
        [SerializeField] private bool newDirectionEachLoad = true;

        [Tooltip("Least angle between the sun's centre and where the player first looks, in degrees. 70 keeps the whole " +
                 "star out of the opening view, so it is found by turning.")]
        [SerializeField, Range(0f, 180f)] private float clearOfStartView = 70f;

        [Tooltip("Greatest angle between the sun's centre and where the player first looks, in degrees. Below 180 keeps " +
                 "the sun from sitting behind the player, where it lights only the side of everything they look at; from " +
                 "the side, every ship and rock shows a lit half and a dark half.")]
        [SerializeField, Range(0f, 180f)] private float furthestFromStartView = 125f;

        [Tooltip("Highest the sun sits above or below the sector's plane, in degrees.")]
        [SerializeField, Range(0f, 90f)] private float maxElevation = 50f;

        [Header("Darkness")]
        [Tooltip("How much darker the sides of ships and rocks away from the sun are. It also pulls the mid tone towards the " +
                 "shadow, so light falls off fast past the edge facing the sun. 0 keeps their materials' own tones.")]
        [SerializeField, Range(0f, 1f)] private float shadowDarkening = 0.8f;

        [Tooltip("How much the pale rim on the edges of ships and rocks is dimmed.")]
        [SerializeField, Range(0f, 1f)] private float rimDimming = 0.8f;

        [Tooltip("How much darker the sky is: nebula, stars and backdrop. The sun keeps its brightness.")]
        [SerializeField, Range(0f, 1f)] private float skyDarkening = 0.6f;

        [Tooltip("How much darker the ambient light is. It lifts the shadowed sides of anything not drawn in the comic look, " +
                 "such as Authority ships.")]
        [SerializeField, Range(0f, 1f)] private float ambientDarkening = 0.85f;

        private SectorSpace space;
        private bool started;
        private Quaternion authoredRotation;

        private bool ambientStored;
        private Color ambientSky;
        private Color ambientEquator;
        private Color ambientGround;

        /// <summary>World direction towards the sun this run.</summary>
        public Vector3 TowardsSun => -transform.forward;

        /// <summary>
        /// A direction towards the sun from a seed: even over the band within <paramref name="maxElevation"/> degrees of the
        /// sector's plane, and between <paramref name="nearestAngle"/> and <paramref name="furthestAngle"/> degrees from
        /// <paramref name="startView"/>. When no candidate fits, the one that misses the range least. Pure, so the
        /// self-test checks it without a scene.
        /// </summary>
        public static Vector3 PickDirection(uint seed, Vector3 startView, float nearestAngle, float furthestAngle, float maxElevation)
        {
            var random = new SectorRandom(seed);
            Vector3 view = startView.sqrMagnitude > 1e-6f ? startView.normalized : Vector3.forward;
            float band = Mathf.Sin(Mathf.Clamp(maxElevation, 0f, 90f) * Mathf.Deg2Rad);

            Vector3 closest = Vector3.forward;
            float closestMiss = float.MaxValue;
            for (int i = 0; i < Attempts; ++i)
            {
                // Height uniform in the sine of elevation spreads directions evenly over the band's area.
                float height = random.Range(-band, band);
                float azimuth = random.Range(0f, 2f * Mathf.PI);
                float across = Mathf.Sqrt(Mathf.Max(0f, 1f - height * height));
                var direction = new Vector3(Mathf.Sin(azimuth) * across, height, Mathf.Cos(azimuth) * across);

                float angle = Vector3.Angle(direction, view);
                float miss = Mathf.Max(nearestAngle - angle, angle - furthestAngle, 0f);
                if (miss <= 0f)
                    return direction;

                if (miss < closestMiss)
                {
                    closestMiss = miss;
                    closest = direction;
                }
            }

            return closest;
        }

        // Darkness first applies in Start, once the loaded scene is the active one and its render settings are the ones
        // in use; after that, whenever the component is enabled again or a setting changes in Play Mode.
        private void OnEnable()
        {
            if (started)
                ApplyDarkness();
        }

        private void OnDisable()
        {
            ClearDarkness();
        }

        private void OnValidate()
        {
            if (started && isActiveAndEnabled)
                ApplyDarkness();
        }

        // Start, not Awake: the sector space has drawn the run's seed, and the camera stands where the player first looks.
        private void Start()
        {
            started = true;
            authoredRotation = transform.rotation;
            space = SectorSpace.Active;
            if (space != null)
                space.SeedChanged += Aim;

            Aim();
            ApplyDarkness();
        }

        private void OnDestroy()
        {
            if (space != null)
                space.SeedChanged -= Aim;
        }

        // Runs from Start and on a seed change, both after the authored rotation is known.
        private void Aim()
        {
            if (!newDirectionEachLoad)
            {
                transform.rotation = authoredRotation;
                return;
            }

            uint seed = space != null
                ? SectorHash.Combine(space.Seed, SunStream)
                : SectorHash.Mix(unchecked((uint)DateTime.UtcNow.Ticks ^ (uint)Environment.TickCount));
            Vector3 towardsSun = PickDirection(seed, StartView(), clearOfStartView, Mathf.Max(clearOfStartView, furthestFromStartView), maxElevation);
            transform.rotation = Quaternion.LookRotation(-towardsSun);
        }

        // Where the player first looks: the main camera, else straight ahead from the arrival point.
        private static Vector3 StartView()
        {
            Camera view = Camera.main;
            return view != null ? view.transform.forward : Vector3.forward;
        }

        private void ApplyDarkness()
        {
            Shader.SetGlobalVector(ShadeId, new Vector4(shadowDarkening, skyDarkening, rimDimming, 0f));

            if (!ambientStored)
            {
                ambientSky = RenderSettings.ambientSkyColor;
                ambientEquator = RenderSettings.ambientEquatorColor;
                ambientGround = RenderSettings.ambientGroundColor;
                ambientStored = true;
            }

            float keep = 1f - ambientDarkening;
            RenderSettings.ambientSkyColor = ambientSky * keep;
            RenderSettings.ambientEquatorColor = ambientEquator * keep;
            RenderSettings.ambientGroundColor = ambientGround * keep;
        }

        // Render settings belong to the active scene. While this scene unloads, another may already be active, and its
        // ambient light must not be touched.
        private void ClearDarkness()
        {
            Shader.SetGlobalVector(ShadeId, Vector4.zero);
            if (!ambientStored)
                return;

            if (gameObject.scene == SceneManager.GetActiveScene())
            {
                RenderSettings.ambientSkyColor = ambientSky;
                RenderSettings.ambientEquatorColor = ambientEquator;
                RenderSettings.ambientGroundColor = ambientGround;
            }

            ambientStored = false;
        }
    }
}
