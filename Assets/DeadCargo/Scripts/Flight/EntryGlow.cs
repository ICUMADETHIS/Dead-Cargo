using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCargo.Flight
{
    /// <summary>
    /// Air heated by the ship's speed, shown as a shell of plasma round the hull: brightest at the bow, in cel bands, with
    /// streaks flowing back along the flanks, and a warm light on the hull. Follows <see cref="AtmosphereFlight.Glow"/>,
    /// eased so it swells and fades rather than flickers. The shell is fitted to the hull when play starts. Play Mode only.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AtmosphereFlight))]
    public sealed class EntryGlow : MonoBehaviour
    {
        /// <summary>The shader the shell is drawn with.</summary>
        public const string ShaderName = "DeadCargo/EntryGlow";

        private const string ShellName = "Entry Glow";
        private const string LightName = "Entry Glow Light";

        // Below this the glow is off, so nothing is drawn in normal flight.
        private const float VisibleGlow = 0.002f;

        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int TravelId = Shader.PropertyToID("_Travel");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

        [Tooltip("The shell's shader. Set by Set Up Sector.")]
        [SerializeField] private Shader shader;

        [Tooltip("Size of the shell round the hull, as a multiple of the hull's size.")]
        [SerializeField, Range(1f, 2f)] private float shellScale = 1.3f;

        [Tooltip("How far the shell reaches ahead of the hull, as a share of the hull's length.")]
        [SerializeField, Range(0f, 1f)] private float bowReach = 0.3f;

        [Tooltip("Brightness of the shell at full glow.")]
        [SerializeField, Range(0f, 4f)] private float brightness = 1.6f;

        [Tooltip("How quickly the glow follows the heating, per second.")]
        [SerializeField, Range(0.5f, 20f)] private float response = 4f;

        [Tooltip("Brightest the light on the hull gets.")]
        [SerializeField, Min(0f)] private float lightIntensity = 4f;

        [Tooltip("Colour of the light on the hull.")]
        [SerializeField] private Color lightColor = new Color(1f, 0.55f, 0.25f);

        private AtmosphereFlight flight;
        private MeshRenderer shell;
        private Light hullLight;
        private Material material;
        private MaterialPropertyBlock properties;
        private float glow;

        /// <summary>The glow shown now, 0 to 1.</summary>
        public float Shown => glow;

        /// <summary>True when the shell's shader is set.</summary>
        public bool HasShader => shader != null;

        private void Awake()
        {
            flight = GetComponent<AtmosphereFlight>();
            if (shader == null)
                shader = Shader.Find(ShaderName);
        }

        // Start, not Awake: the ship's parts have taken their places.
        private void Start()
        {
            if (shader == null)
            {
                Debug.LogWarning($"Entry Glow has no shader, so heating shows nothing. Run Set Up Sector.", this);
                enabled = false;
                return;
            }

            Build();
        }

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }

        private void OnDisable()
        {
            glow = 0f;
            Show(false);
        }

        private void LateUpdate()
        {
            glow = Mathf.Lerp(glow, flight.Glow, 1f - Mathf.Exp(-response * Time.deltaTime));
            bool visible = glow > VisibleGlow;
            Show(visible);
            if (!visible)
                return;

            properties.SetFloat(GlowId, glow);
            properties.SetFloat(BrightnessId, brightness);
            properties.SetVector(TravelId, flight.Travel);
            shell.SetPropertyBlock(properties);
            hullLight.intensity = glow * lightIntensity;
        }

        private void Show(bool visible)
        {
            if (shell != null && shell.enabled != visible)
                shell.enabled = visible;
            if (hullLight != null && hullLight.enabled != visible)
                hullLight.enabled = visible;
        }

        private void Build()
        {
            Bounds hull = HullBounds();

            // A unit sphere, 0.5 in radius, which the shader's inside test relies on.
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.name = ShellName;
            sphere.layer = gameObject.layer;

            Transform shellTransform = sphere.transform;
            shellTransform.SetParent(transform, false);
            Vector3 size = hull.size * shellScale;
            size.z *= 1f + bowReach;
            shellTransform.localPosition = hull.center + Vector3.forward * (hull.size.z * bowReach * 0.5f);
            shellTransform.localRotation = Quaternion.identity;
            shellTransform.localScale = size;

            material = new Material(shader) { name = ShellName, hideFlags = HideFlags.DontSave };
            shell = sphere.GetComponent<MeshRenderer>();
            shell.sharedMaterial = material;
            shell.shadowCastingMode = ShadowCastingMode.Off;
            shell.receiveShadows = false;
            shell.lightProbeUsage = LightProbeUsage.Off;
            shell.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shell.enabled = false;
            properties = new MaterialPropertyBlock();

            var lightObject = new GameObject(LightName) { layer = gameObject.layer };
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = hull.center + Vector3.forward * (hull.extents.z * 1.2f);
            hullLight = lightObject.AddComponent<Light>();
            hullLight.type = LightType.Point;
            hullLight.color = lightColor;
            hullLight.range = hull.size.magnitude * 1.5f;
            hullLight.shadows = LightShadows.None;
            hullLight.intensity = 0f;
            hullLight.enabled = false;
        }

        // The hull's extent in the ship's own space, from the meshes drawn now.
        private Bounds HullBounds()
        {
            var bounds = new Bounds();
            bool found = false;
            foreach (Renderer part in GetComponentsInChildren<Renderer>())
            {
                if (!(part is MeshRenderer || part is SkinnedMeshRenderer) || !part.enabled || !part.gameObject.activeInHierarchy)
                    continue;

                Bounds world = part.bounds;
                Vector3 min = world.min;
                Vector3 max = world.max;
                for (int corner = 0; corner < 8; ++corner)
                {
                    var point = new Vector3((corner & 1) == 0 ? min.x : max.x, (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z);
                    Vector3 local = transform.InverseTransformPoint(point);
                    if (found)
                    {
                        bounds.Encapsulate(local);
                    }
                    else
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        found = true;
                    }
                }
            }

            return found ? bounds : new Bounds(Vector3.zero, Vector3.one * 10f);
        }
    }
}
