using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// The comic HUD's building block: a sticker leaning like italic type, filled flat, with a bold ink outline and a hard
    /// ink shadow. Below a fill of 1 it is a bar: the face colour up to that share of its width, the track colour after.
    /// Its rect is the outline's outer edge; the shadow falls outside it, down and to the right. Edges are anti-aliased.
    /// </summary>
    internal sealed class HudSticker : HudShapeGraphic
    {
        /// <summary>The ink of every outline and shadow.</summary>
        internal static readonly Color Ink = new Color32(4, 4, 6, 255);

        /// <summary>Lean for each unit of height, tan 14°, as the strip's stickers lean.</summary>
        internal const float DefaultSlant = 0.25f;

        // Corners and edge softness of the shape being added; the HUD is built on the main thread only.
        private static readonly Vector2[] Corners = new Vector2[4];
        private static readonly float[] Soft = new float[4];

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

        protected override void Build(VertexHelper mesh, Rect rect, float feather)
        {
            float lean = rect.height * slant;

            if (shadow != Vector2.zero)
                AddLeaning(mesh, rect.xMin + shadow.x, rect.yMin + shadow.y, rect.width, rect.height, lean, Ink, feather, true, true);

            if (outline > 0f)
                AddLeaning(mesh, rect.xMin, rect.yMin, rect.width, rect.height, lean, Ink, feather, true, true);

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

            // Where fill meets track the edge stays hard, so the two meet without a seam.
            if (filled > 0f)
                AddLeaning(mesh, left, bottom, filled + faceLean, height, faceLean, face, feather, true, filled >= width);

            if (filled < width)
                AddLeaning(mesh, left + filled, bottom, width - filled + faceLean, height, faceLean, track, feather, filled <= 0f, true);
        }

        // A parallelogram in the box from (x, y), leaning right by lean at the top. Its left or right edge can be left hard.
        private static void AddLeaning(VertexHelper mesh, float x, float y, float width, float height, float lean, Color tint,
            float feather, bool softLeft, bool softRight)
        {
            float span = width - lean;
            if (height <= 0f || span <= 0f)
                return;

            // Left, top, right and bottom edges, in that order.
            Corners[0] = new Vector2(x, y);
            Corners[1] = new Vector2(x + lean, y + height);
            Corners[2] = new Vector2(x + width, y + height);
            Corners[3] = new Vector2(x + width - lean, y);

            // A sliver thinner than the feather keeps a solid core at least half its thickness.
            float across = span * height / Mathf.Sqrt(height * height + lean * lean);
            float sides = Mathf.Min(feather * 0.5f, across * 0.25f);
            float ends = Mathf.Min(feather * 0.5f, height * 0.25f);
            Soft[0] = softLeft ? sides : 0f;
            Soft[1] = ends;
            Soft[2] = softRight ? sides : 0f;
            Soft[3] = ends;
            HudShapes.AddPolygon(mesh, Corners, 4, tint, Soft);
        }
    }
}
