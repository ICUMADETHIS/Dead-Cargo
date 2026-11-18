using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// The steering cursor over the reticle: a small amber dot ringed in ink, inside the reticle's ring while the player
    /// steers straight. When <see cref="HudSettings.ShowSteeringLine"/> is on, an inked line runs out to it once it clears
    /// the ring. Rebuilt only when the cursor moves by half a reference pixel or more, or the setting changes.
    /// </summary>
    internal sealed class HudSteeringCursor : HudShapeGraphic
    {
        private const float DotRadius = 3.5f;
        private const float DotInk = 1.5f;
        private const float LineWidth = 2f;
        private const float LineInk = 1.5f;
        private const float LineAlpha = 0.65f;
        private const float Step = 0.5f;

        private Vector2 offset;

        /// <summary>The cursor's offset from the screen centre, in reference pixels.</summary>
        internal void SetOffset(Vector2 value)
        {
            value = new Vector2(Mathf.Round(value.x / Step), Mathf.Round(value.y / Step)) * Step;
            if (value == offset)
                return;

            offset = value;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            HudSettings.Changed += SetVerticesDirty;
        }

        protected override void OnDisable()
        {
            HudSettings.Changed -= SetVerticesDirty;
            base.OnDisable();
        }

        protected override void Build(VertexHelper mesh, Rect rect, float feather)
        {
            Vector2 centre = rect.center;
            Vector2 dot = centre + offset;
            float dotOuter = DotRadius + DotInk;
            float distance = offset.magnitude;

            if (HudSettings.ShowSteeringLine && distance > HudReticle.Reach + dotOuter)
            {
                Vector2 along = offset / distance;
                Vector2 from = centre + along * HudReticle.Reach;
                Vector2 to = dot - along * dotOuter;
                Color ink = HudSticker.Ink;
                ink.a = LineAlpha;
                Color stroke = StatusStripView.Figure;
                stroke.a = LineAlpha;
                HudShapes.AddStroke(mesh, from, to, LineWidth + LineInk * 2f, ink, feather);
                HudShapes.AddStroke(mesh, from, to, LineWidth, stroke, feather);
            }

            HudShapes.AddDisc(mesh, dot, dotOuter, HudSticker.Ink, feather);
            HudShapes.AddDisc(mesh, dot, DotRadius, StatusStripView.Accent, feather);
        }
    }
}
