using CW.SimpleShaderSystem;
using DeadCargo.Visuals;
using SpaceGraphicsToolkit.LightAndShadow;
using SpaceGraphicsToolkit.Volumetrics;
using Unity.Mathematics;
using UnityEngine;

namespace DeadCargo.Sectors.Far
{
    /// <summary>
    /// Space beyond the main camera's reach, drawn as a scale copy behind it: the first part of the far world.
    /// <para>
    /// <b>A scale copy.</b> The far world is space at 1:<see cref="Scale"/>, on its own layer. Perspective does not depend
    /// on scale, so a 500 km planet 2,000 km away looks exactly like a 500 unit sphere 2,000 units away.
    /// </para>
    /// <para>
    /// <b>A far camera.</b> It draws the sky and then the far world into a screen-sized HDR texture, before the main
    /// camera. The main camera shows that texture as its sky (a per-camera <see cref="Skybox"/> with the Far Space
    /// Backdrop shader), so the near world is drawn over it, and the sun and atmospheres keep their full brightness for
    /// bloom. Just before it draws, the far camera copies the main camera's position, turn and field of view, so the two
    /// never disagree. Every other game camera leaves the layer out.
    /// </para>
    /// <para>
    /// <b>One place for both cameras.</b> Planet Forge works out detail, atmosphere and ocean from the main camera, so the
    /// far camera stands exactly where the main camera is and the far world moves to match: its origin follows the camera
    /// at (1 - 1/scale) of the camera's motion, so flying a kilometre moves a metre through the far world. This runs early
    /// in LateUpdate, before Planet Forge reads its planets. The object must stay unrotated and unscaled.
    /// </para>
    /// <para>
    /// <b>A far sun.</b> A camera ignores lights on layers it does not draw, and Planet Forge lights a planet only with
    /// lights whose culling mask holds the planet's layer. So the far world has its own directional light on its own
    /// layer, carrying Planet Forge's <see cref="SgtLight"/>, that copies the scene's sun (<see cref="SceneSun"/>) every
    /// frame: its turn, colour and brightness. It is on only while the far camera draws, and the scene's sun leaves the far
    /// layer out of its culling mask, so nothing is lit twice.
    /// </para>
    /// Play Mode only. Disabled, the main camera draws the scene's own sky again, exactly as without far space.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class FarSpace : MonoBehaviour
    {
        /// <summary>The name of the layer only the far camera draws.</summary>
        public const string LayerName = "Far Space";

        /// <summary>The shader that shows the far camera's picture as the main camera's sky.</summary>
        public const string BackdropShaderName = "DeadCargo/Far Space Backdrop";

        private const string WorldName = "Far World";
        private const string CameraName = "Far Camera";
        private const string SunName = "Far Sun";
        // A float depth buffer keeps distant terrain steady while the near clip comes down to the ground.
        private const int DepthBits = 32;
        private const int FallbackSamples = 4;

        private static readonly int FarTextureId = Shader.PropertyToID("_MainTex");

        [Tooltip("Metres of real space in one unit of the far world. At 1000 a unit is a kilometre, so a planet 500 units " +
                 "in radius stands for one 500 km in radius.")]
        [SerializeField, Min(1f)] private float scale = 1000f;

        [Tooltip("The layer only the far camera draws, named Far Space. Set by Add Planets in the God Menu Project tab.")]
        [SerializeField] private int layer = -1;

        [Tooltip("The Far Space Backdrop shader, which shows the far camera's picture as the main camera's sky. Set by Add " +
                 "Planets, so builds include it.")]
        [SerializeField] private Shader backdropShader;

        [Tooltip("Nearest the far camera draws, in far-world units (kilometres at the default scale).")]
        [SerializeField, Min(0.01f)] private float nearClip = 1f;

        [Tooltip("Furthest the far camera draws, in far-world units. Raised on its own to reach the furthest body.")]
        [SerializeField, Min(1f)] private float farClip = 200000f;

        [Tooltip("How bright the sun is on planets, against the scene's sun. The main camera's post-processing brightens the " +
                 "whole picture by 1.5 stops for Dead Cargo's look, and Planet Forge's planets were made without it, so at 1 " +
                 "they wash out to white and bloom into a glow. Lower keeps their colours.")]
        [SerializeField, Range(0f, 2f)] private float planetLight = 0.35f;

        [Tooltip("Atmospheres and clouds are drawn at this fraction of the screen's width and height: 1 full size, 2 half. " +
                 "Above 1 they turn blocky and leave a halo round each planet's edge.")]
        [SerializeField, Range(1, 4)] private int volumetricDownscale = 1;

        [Tooltip("Atmospheres and clouds are drawn in a pattern over this many frames a side (3 draws a ninth of their pixels " +
                 "each frame) and built up over time. Cheaper, and needs Volumetric Smoothing.")]
        [SerializeField, Range(1, 8)] private int volumetricCheckerboard = 3;

        [Tooltip("How much each frame of atmosphere and cloud blends with the last ones, 0 to 1. Smooths away the grain of their " +
                 "sampling; 0 leaves it speckled.")]
        [SerializeField, Range(0f, 1f)] private float volumetricSmoothing = 0.6f;

        [Tooltip("Smooth the planets' edges. Uses the quality level's anti-aliasing, or 4 samples when it has none.")]
        [SerializeField] private bool antiAliasing = true;

        private Transform world;
        private Camera farCamera;
        private Light farSun;
        private RenderTexture target;
        private Material backdrop;
        private SgtVolumeManager volumeManager;
        private bool ownsVolumeManager;
        private bool valid;
        private int mask;
        private int hiddenMask;
        private float reach;
        private float nearOverride;

        // The main camera whose sky this replaces, and what it had before.
        private Camera view;
        private CameraClearFlags viewClearFlags;
        private Skybox viewSkybox;
        private bool addedSkybox;
        private Material viewSkyboxMaterial;
        private bool viewSkyboxEnabled;

        // The scene's sun the far sun copies, and the far layer's bit in its culling mask before it was taken out.
        private Light sun;
        private int sunFarBit;

        /// <summary>The far space of the loaded scene, or null.</summary>
        public static FarSpace Active { get; private set; }

        /// <summary>Metres of real space in one far-world unit.</summary>
        public float Scale => scale;

        /// <summary>The layer only the far camera draws.</summary>
        public int Layer => layer;

        /// <summary>True when the far space has a layer of its own and its backdrop shader, so it can draw.</summary>
        public bool IsValid => valid;

        /// <summary>The far world's origin. Far content goes under it through <see cref="Adopt"/>.</summary>
        public Transform World => world;

        /// <summary>The far camera, created at load.</summary>
        public Camera FarCamera => farCamera;

        /// <summary>True while the far camera draws behind a main camera.</summary>
        public bool IsDrawing => valid && isActiveAndEnabled && view != null;

        /// <summary>Furthest the far camera draws, in far-world units.</summary>
        public float DrawDistance => Mathf.Max(farClip, reach);

        /// <summary>Moves <paramref name="content"/> into the far world, keeping its position, and onto the far layer.</summary>
        public void Adopt(GameObject content)
        {
            if (!valid || content == null)
                return;

            content.transform.SetParent(world, true);
            SetLayer(content.transform, layer);
        }

        /// <summary>The furthest any far content stands, in far-world units, so the far camera reaches it.</summary>
        public void SetReach(float distance)
        {
            reach = Mathf.Max(0f, distance);
        }

        /// <summary>
        /// Draws from <paramref name="distance"/> far-world units instead of the near clip set here, so ground close below
        /// the ship is not cut away. 0 returns to the set near clip.
        /// </summary>
        public void SetNearClipOverride(float distance)
        {
            nearOverride = Mathf.Max(0f, distance);
        }

        /// <summary>Keeps a layer, such as the planets' invisible ground, out of every game camera, as the far layer is.</summary>
        public void HideLayer(int hiddenLayer)
        {
            if (hiddenLayer > 0 && hiddenLayer < 32)
                hiddenMask |= 1 << hiddenLayer;
        }

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
        }

        private void Awake()
        {
            if (backdropShader == null)
                backdropShader = Shader.Find(BackdropShaderName);

            valid = layer > 0 && layer < 32 && backdropShader != null;
            if (!valid)
            {
                Debug.LogWarning("Far Space needs its own layer and the Far Space Backdrop shader, so nothing far is drawn. " +
                                 "Press Add Planets in the God Menu Project tab.", this);
                return;
            }

            mask = 1 << layer;
            world = new GameObject(WorldName).transform;
            world.SetParent(transform, false);
            world.gameObject.layer = layer;

            backdrop = new Material(backdropShader) { name = WorldName, hideFlags = HideFlags.DontSave };
            farCamera = CreateCamera();
            EnsureVolumeManager();
        }

        // Start, not Awake: the loaded scene is the active one, whose lighting settings these are.
        private void Start()
        {
            if (!valid)
                return;

            // The scene's sun becomes its Sun Source for this run before the far sun exists, so SceneSun, which falls back on
            // the brightest directional light, can never pick the copy.
            Light sceneSun = SceneSun.Light;
            if (RenderSettings.sun == null && sceneSun != null)
                RenderSettings.sun = sceneSun;

            farSun = CreateSun();
        }

        private void OnEnable()
        {
            Active = this;
            if (!valid)
                return;

            Camera.onPreCull += HandlePreCull;
            Camera.onPostRender += HandlePostRender;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= HandlePreCull;
            Camera.onPostRender -= HandlePostRender;
            if (farCamera != null)
                farCamera.enabled = false;

            if (farSun != null)
                farSun.enabled = false;

            ReleaseView();
            ReleaseSun();
            if (Active == this)
                Active = null;
        }

        private void OnDestroy()
        {
            if (farCamera != null)
                farCamera.targetTexture = null;

            if (target != null)
            {
                target.Release();
                Destroy(target);
            }

            if (backdrop != null)
                Destroy(backdrop);
        }

        private void OnValidate()
        {
            if (farCamera != null)
                farCamera.nearClipPlane = nearClip;

            if (volumeManager != null && ownsVolumeManager)
                ApplyVolumetrics();
        }

        // Planet Forge's own demos draw atmospheres and clouds at full size, in a checkerboard smoothed over frames.
        private void ApplyVolumetrics()
        {
            volumeManager.Downscale = volumetricDownscale;
            volumeManager.Checkerboard = volumetricCheckerboard;
            volumeManager.Taa = volumetricSmoothing;
            volumeManager.HighPrecision = true;
        }

        private void LateUpdate()
        {
            if (!valid || farSun == null)
                return;

            Camera main = Camera.main;
            if (main != view)
            {
                ReleaseView();
                if (main != null)
                    TakeView(main);
            }

            farCamera.enabled = view != null;
            CopySun();
            if (view == null)
                return;

            farCamera.depth = view.depth - 1f;
            EnsureTarget(view);
            Follow(view.transform.position);
        }

        // On from here, before Planet Forge reads its lights in LateUpdate, until the far camera has drawn.
        private void CopySun()
        {
            Light source = SceneSun.Light;
            if (source == farSun)
                source = null;

            if (source != sun)
            {
                ReleaseSun();
                sun = source;
                if (sun != null)
                {
                    sunFarBit = sun.cullingMask & mask;
                    sun.cullingMask &= ~mask;
                }
            }

            farSun.enabled = view != null && sun != null;
            if (sun == null)
                return;

            farSun.transform.rotation = sun.transform.rotation;
            farSun.color = sun.color;
            farSun.intensity = sun.intensity * planetLight;
            farSun.useColorTemperature = sun.useColorTemperature;
            farSun.colorTemperature = sun.colorTemperature;
        }

        private void ReleaseSun()
        {
            if (sun != null)
                sun.cullingMask |= sunFarBit;

            sun = null;
        }

        // The far sun lights the far world only; the main camera, which draws next, never sees it.
        private void HandlePostRender(Camera rendered)
        {
            if (rendered == farCamera && farSun != null)
                farSun.enabled = false;
        }

        // The far world's origin, placed so content at a place in the star system stands where the camera sees it, at
        // 1:scale. The camera's system position is divided by the scale in double precision, as cruise carries it
        // thousands of kilometres from the origin.
        private void Follow(Vector3 cameraPosition)
        {
            SectorSpace space = SectorSpace.Active;
            double3 systemPosition = space != null ? space.WorldToSystem(cameraPosition) : (double3)(float3)cameraPosition;
            Vector3 scaled = (float3)(systemPosition / scale);
            world.SetPositionAndRotation(cameraPosition - scaled, Quaternion.identity);
        }

        private void HandlePreCull(Camera rendering)
        {
            if (rendering == farCamera)
            {
                if (view == null)
                    return;

                // The main camera has its final pose for the frame by now, whichever script moved it.
                Transform source = view.transform;
                farCamera.transform.SetPositionAndRotation(source.position, source.rotation);
                farCamera.fieldOfView = view.fieldOfView;
                farCamera.nearClipPlane = nearOverride > 0f ? nearOverride : nearClip;
                farCamera.farClipPlane = DrawDistance;
                return;
            }

            int kept = mask | hiddenMask;
            if (rendering.cameraType == CameraType.Game && (rendering.cullingMask & kept) != 0)
                rendering.cullingMask &= ~kept;
        }

        // One texel of the far picture for each pixel of the main camera, in HDR when the main camera is.
        private void EnsureTarget(Camera main)
        {
            int width = Mathf.Max(1, main.pixelWidth);
            int height = Mathf.Max(1, main.pixelHeight);
            RenderTextureFormat format = main.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            int samples = !antiAliasing ? 1 : QualitySettings.antiAliasing > 1 ? QualitySettings.antiAliasing : FallbackSamples;
            if (target != null && target.width == width && target.height == height && target.format == format &&
                target.antiAliasing == samples)
                return;

            if (target != null)
            {
                farCamera.targetTexture = null;
                target.Release();
                Destroy(target);
            }

            // A multisampled texture is resolved on its own when the backdrop reads it.
            target = new RenderTexture(width, height, DepthBits, format)
            {
                name = CameraName,
                antiAliasing = samples,
                hideFlags = HideFlags.DontSave
            };
            target.Create();
            farCamera.targetTexture = target;
            farCamera.allowHDR = main.allowHDR;
            farCamera.allowMSAA = samples > 1;
            backdrop.SetTexture(FarTextureId, target);
        }

        // The main camera shows the far picture as its sky, through a Skybox of its own, and never draws the far layer.
        private void TakeView(Camera main)
        {
            view = main;
            viewClearFlags = main.clearFlags;
            main.clearFlags = CameraClearFlags.Skybox;
            main.cullingMask &= ~(mask | hiddenMask);

            addedSkybox = !main.TryGetComponent(out viewSkybox);
            if (addedSkybox)
            {
                viewSkybox = main.gameObject.AddComponent<Skybox>();
            }
            else
            {
                viewSkyboxMaterial = viewSkybox.material;
                viewSkyboxEnabled = viewSkybox.enabled;
            }

            viewSkybox.material = backdrop;
            viewSkybox.enabled = true;
            farCamera.depth = main.depth - 1f;
        }

        private void ReleaseView()
        {
            if (view != null)
                view.clearFlags = viewClearFlags;

            if (viewSkybox != null)
            {
                if (addedSkybox)
                {
                    Destroy(viewSkybox);
                }
                else
                {
                    viewSkybox.material = viewSkyboxMaterial;
                    viewSkybox.enabled = viewSkyboxEnabled;
                }
            }

            viewSkybox = null;
            view = null;
        }

        private Camera CreateCamera()
        {
            var cameraObject = new GameObject(CameraName);
            cameraObject.transform.SetParent(transform, false);
            cameraObject.SetActive(false);

            var created = cameraObject.AddComponent<Camera>();
            created.enabled = false;
            created.clearFlags = CameraClearFlags.Skybox;
            created.backgroundColor = Color.black;
            created.cullingMask = mask;
            created.renderingPath = RenderingPath.Forward;
            created.allowMSAA = false;
            created.useOcclusionCulling = false;
            created.nearClipPlane = nearClip;
            created.farClipPlane = farClip;
            created.depthTextureMode = DepthTextureMode.Depth;

            // Planet Forge's atmospheres and clouds, and its oceans' view of what lies beneath them.
            cameraObject.AddComponent<SgtVolumeCamera>();
            cameraObject.AddComponent<CameraOpaqueTextureBIRP>();
            cameraObject.SetActive(true);
            return created;
        }

        private Light CreateSun()
        {
            var sunObject = new GameObject(SunName);
            sunObject.layer = layer;
            sunObject.transform.SetParent(transform, false);
            sunObject.SetActive(false);

            var created = sunObject.AddComponent<Light>();
            created.type = LightType.Directional;
            created.cullingMask = mask;
            created.shadows = LightShadows.None;
            created.renderMode = LightRenderMode.ForcePixel;
            created.enabled = false;
            sunObject.AddComponent<SgtLight>();
            sunObject.SetActive(true);
            return created;
        }

        private void EnsureVolumeManager()
        {
            if (SgtVolumeManager.Instances.Count > 0)
            {
                volumeManager = SgtVolumeManager.Instances.First.Value;
                return;
            }

            volumeManager = gameObject.AddComponent<SgtVolumeManager>();
            ownsVolumeManager = true;
            ApplyVolumetrics();
        }

        private static void SetLayer(Transform node, int newLayer)
        {
            node.gameObject.layer = newLayer;
            for (int i = 0; i < node.childCount; ++i)
                SetLayer(node.GetChild(i), newLayer);
        }
    }
}
