using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// A HUD marker shape in the comic look: a diamond over a target, or a notched arrowhead on the screen edge pointing
    /// towards one, filled with the graphic's colour inside a bold ink outline. The arrow points along +x; the marker layer
    /// rotates it. The outline fades with the colour's alpha.
    /// </summary>
    internal sealed class HudMarkerGlyph : HudShapeGraphic
    {
        // The arrow's notch, as a share of its length back from the tip, and its height against its length.
        private const float ArrowNotch = 0.71f;
        private const float ArrowSpread = 1.25f;

        private static readonly Vector2[] Shape = new Vector2[4];
        private static readonly Vector2[] Outline = new Vector2[4];

        private bool isArrow;
        private float outline = 3f;

        /// <summary>Half the glyph's extent along its longest side, ink included, in local units.</summary>
        internal float Reach => rectTransform.rect.width * 0.5f * (isArrow ? ArrowSpread : 1f) + outline;

        internal void SetShape(bool arrow, float outlineWidth)
        {
            outlineWidth = Mathf.Max(0f, outlineWidth);
            if (isArrow == arrow && Mathf.Approximately(outline, outlineWidth))
                return;

            isArrow = arrow;
            outline = outlineWidth;
            SetVerticesDirty();
        }

        protected override void Build(VertexHelper mesh, Rect rect, float feather)
        {
            Vector2 centre = rect.center;
            float half = rect.width * 0.5f;

            if (isArrow)
            {
                // Tip first, so the notched shape fans from it.
                float spread = half * ArrowSpread;
                Shape[0] = centre + new Vector2(half, 0f);
                Shape[1] = centre + new Vector2(-half, spread);
                Shape[2] = centre + new Vector2(half - 2f * half * ArrowNotch, 0f);
                Shape[3] = centre + new Vector2(-half, -spread);
            }
            else
            {
                Shape[0] = centre + new Vector2(0f, half);
                Shape[1] = centre + new Vector2(half, 0f);
                Shape[2] = centre + new Vector2(0f, -half);
                Shape[3] = centre + new Vector2(-half, 0f);
            }

            Color fill = color;
            if (outline > 0f)
            {
                Color ink = HudSticker.Ink;
                ink.a *= fill.a;
                HudShapes.Expand(Shape, 4, outline, Outline);
                HudShapes.AddPolygon(mesh, Outline, 4, ink, feather);
            }

            HudShapes.AddPolygon(mesh, Shape, 4, fill, feather);
        }
    }
}
