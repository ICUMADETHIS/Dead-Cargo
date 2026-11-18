using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// The aiming reticle at the screen centre: one small inked ring, kept slight so it takes up little of the screen. It
    /// never moves; <see cref="HudSteeringCursor"/> draws the steering dot, which sits inside it while the ship flies
    /// straight.
    /// </summary>
    internal sealed class HudReticle : HudShapeGraphic
    {
        /// <summary>How far the ring reaches from the centre, ink included, in reference pixels.</summary>
        internal const float Reach = Radius + StrokeWidth * 0.5f + InkWidth;

        private const float Radius = 9f;
        private const float StrokeWidth = 2f;
        private const float InkWidth = 1.5f;

        protected override void Build(VertexHelper mesh, Rect rect, float feather)
        {
            HudShapes.AddRing(mesh, rect.center, Radius, StrokeWidth + InkWidth * 2f, HudSticker.Ink, feather);
            HudShapes.AddRing(mesh, rect.center, Radius, StrokeWidth, StatusStripView.Figure, feather);
        }
    }
}
