using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// The comic HUD's building block: a sticker leaning like italic type, filled flat, with a bold ink outline and a hard
    /// ink shadow. Below a fill of 1 it is a bar: the face colour up to that share of its width, the track colour after.
    /// Its rect is the outline's outer edge; the shadow falls outside it, down and to the right.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class HudSticker : MaskableGraphic
    {
        /// <summary>The ink of every outline and shadow.</summary>
        internal static readonly Color Ink = new Color32(4, 4, 6, 255);

        /// <summary>Lean for each unit of height, tan 14°, as the strip's stickers lean.</summary>
        internal const float DefaultSlant = 0.25f;

        private Color face = new Color32(24, 27, 34, 245);
        private Color track = new Color32(10, 11, 14, 255);
        private float fill = 1f;
        private float outline = 3f;
        private Vector2 shadow = new Vector2(6f, -6f);
        private float slant = DefaultSlant;

        /// <summary>Sets the sticker's look in one go.</summary>
        internal void Configure(Color faceColor, float outlineWidth, Vector2 shadowOffset, float lean)
        {
            face = faceColor;
            outline = Mathf.Max(0f, outlineWidth);
            shadow = shadowOffset;
            slant = lean;
            SetVerticesDirty();
        }

        internal void SetFace(Color value)
        {
            if (face == value)
                return;

            face = value;
            SetVerticesDirty();
        }

        internal void SetTrack(Color value)
        {
            if (track == value)
                return;

            track = value;
            SetVerticesDirty();
        }

        /// <summary>How much of the width the face colour fills, 0 to 1.</summary>
        internal void SetFill(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Abs(fill - value) < 0.002f)
                return;

            fill = value;
            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            useLegacyMeshGeneration = false;
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float lean = rect.height * slant;

            if (shadow != Vector2.zero)
                AddLeaning(mesh, rect.xMin + shadow.x, rect.yMin + shadow.y, rect.width, rect.height, lean, Ink);

            if (outline > 0f)
                AddLeaning(mesh, rect.xMin, rect.yMin, rect.width, rect.height, lean, Ink);

            // The face sits inside the outline, its edges parallel to the outline's.
            float height = rect.height - outline * 2f;
            float across = outline * Mathf.Sqrt(1f + slant * slant);
            float width = rect.width - lean - across * 2f;
            if (height <= 0f || width <= 0f)
                return;

            float left = rect.xMin + slant * outline + across;
            float bottom = rect.yMin + outline;
            float faceLean = height * slant;
            float filled = width * fill;
            if (filled > 0f)
                AddLeaning(mesh, left, bottom, filled + faceLean, height, faceLean, face);

            if (filled < width)
                AddLeaning(mesh, left + filled, bottom, width - filled + faceLean, height, faceLean, track);
        }

        // A parallelogram in the box from (x, y), leaning right by lean at the top.
        private static void AddLeaning(VertexHelper mesh, float x, float y, float width, float height, float lean, Color tint)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(new Vector2(x, y), tint, Vector2.zero);
            mesh.AddVert(new Vector2(x + lean, y + height), tint, Vector2.zero);
            mesh.AddVert(new Vector2(x + width, y + height), tint, Vector2.zero);
            mesh.AddVert(new Vector2(x + width - lean, y), tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
