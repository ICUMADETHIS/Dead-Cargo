using UnityEngine;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// Depth for every comic surface in the scene at once: with distance, ships and rocks fade towards the sky's void
    /// colour, smoothly or in a few crisp bands, so near and far read apart at a glance, then dissolve into the sky
    /// before the draw distance, so nothing pops in or out at the edge of sight. Sets the shader globals that
    /// <c>Shaders/ComicSurface.shader</c> reads; without this object surfaces are unchanged. Lives in the Environment
    /// folder, one per scene.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SpaceDepth : MonoBehaviour
    {
        // The globals the comic surface reads. ComicSurface.shader declares them with these names.
        private static readonly int DepthId = Shader.PropertyToID("_SpaceDepth");
        private static readonly int ColorId = Shader.PropertyToID("_SpaceDepthColor");
        private static readonly int DissolveId = Shader.PropertyToID("_SpaceDepthDissolve");
        private static readonly int SkyVoidId = Shader.PropertyToID(SpaceSkyProperties.VoidColor);

        private const float FallbackEnd = 3000f;

        [Tooltip("Distance where surfaces start to fade, in metres.")]
        [SerializeField, Min(0f)] private float fadeStart = 400f;

        [Tooltip("Bands the fade takes on its way. Only seen with Band Softness below 1.")]
        [SerializeField, Range(1, 16)] private int steps = 4;

        [Tooltip("How the bands meet: 1 blends them into one smooth fade; 0 gives crisp comic bands, each edge one pixel " +
                 "wide.")]
        [SerializeField, Range(0f, 1f)] private float bandSoftness = 1f;

        [Tooltip("How far the fade goes towards the void colour, 0 to 1. 0 turns the colour fade off.")]
        [SerializeField, Range(0f, 1f)] private float strength = 0.8f;

        [Tooltip("Distance over which objects dissolve away before the end, in metres. 0 lets them stop at the edge.")]
        [SerializeField, Min(0f)] private float dissolveRange = 700f;

        [Tooltip("Where objects are gone, in metres. 0 uses the main camera's far clip, where the asteroid field stops too.")]
        [SerializeField, Min(0f)] private float fadeEnd;

        [Tooltip("Fade towards the sky's own void colour. Off uses the colour below.")]
        [SerializeField] private bool useSkyColour = true;

        [Tooltip("The colour far surfaces fade towards, when not the sky's.")]
        [SerializeField] private Color colour = new Color(0.006f, 0.008f, 0.016f);

        [Tooltip("Show the fade in the Scene view outside Play Mode. Off by default, as the Scene view often looks " +
                 "across the whole sector, where everything would dissolve.")]
        [SerializeField] private bool previewInEditor;

        /// <summary>Where objects are gone, in metres, as last applied. Zero while the fade is off.</summary>
        public float End { get; private set; }

        /// <summary>Where the colour fade starts, in metres, as last applied.</summary>
        public float Start => fadeStart;

        private void OnEnable()
        {
            Apply();
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
                Apply();
        }

        // Every frame, as the camera's far clip, or the sky, may change.
        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (!Application.isPlaying && !previewInEditor)
            {
                Clear();
                return;
            }

            Camera view = Camera.main;
            float end = fadeEnd > 0f ? fadeEnd : view != null ? view.farClipPlane : FallbackEnd;
            float start = Mathf.Min(fadeStart, end);
            float dissolveStart = Mathf.Max(start, end - dissolveRange);

            // The colour fade finishes where dissolving begins, so objects dissolve at their faintest.
            Shader.SetGlobalVector(DepthId, new Vector4(start, dissolveStart, steps, strength));
            Shader.SetGlobalVector(DissolveId, dissolveRange > 0f ? new Vector4(dissolveStart, end, 0f, 0f) : Vector4.zero);

            // The colour's alpha carries the band softness; SetGlobalColor converts only the colour channels.
            Color fade = FadeColour();
            fade.a = bandSoftness;
            Shader.SetGlobalColor(ColorId, fade);
            End = end;
        }

        private Color FadeColour()
        {
            Material sky = RenderSettings.skybox;
            if (useSkyColour && sky != null && sky.HasProperty(SkyVoidId))
                return sky.GetColor(SkyVoidId);

            return colour;
        }

        private void Clear()
        {
            Shader.SetGlobalVector(DepthId, Vector4.zero);
            Shader.SetGlobalVector(DissolveId, Vector4.zero);
            End = 0f;
        }
    }
}
