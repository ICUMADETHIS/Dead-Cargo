using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// A comic starburst: spikes round a disc, inked, with a smaller burst of the jump's colour inside, filling its rect. It
    /// marks where the ship jumped from. <see cref="SetAge"/> pops it out, spins it and shrinks it away by scaling the rect,
    /// so the mesh is built once.
    /// </summary>
    internal sealed class HudBurst : HudShapeGraphic
    {
        private const int Spikes = 12;
        private const float InnerShare = 0.55f;
        private const float CoreShare = 0.55f;
        private const float InkWidth = 7f;

        // Its life, in seconds from the jump.
        private const float Appear = 0.05f;
        private const float Grown = 0.17f;
        private const float Hold = 0.38f;
        private const float Gone = 0.6f;
        private const float Overshoot = 1.15f;
        private const float SpinPerSecond = 40f;

        private static readonly Color Face = new Color32(243, 238, 228, 255);
        private static readonly Color Core = new Color32(116, 206, 255, 255);
        private static readonly Vector2[] Corners = new Vector2[HudShapes.MaxCorners];
        private static readonly float[] Soft = new float[HudShapes.MaxCorners];

        /// <summary>Scales and turns the burst for its age, in seconds from the jump. Hidden before it appears and once gone.</summary>
        internal void SetAge(float seconds)
        {
            float scale;
            if (seconds < Appear || seconds >= Gone)
                scale = 0f;
            else if (seconds < Grown)
                scale = PopOut((seconds - Appear) / (Grown - Appear));
            else if (seconds < Hold)
                scale = 1f;
            else
                scale = 1f - Mathf.Clamp01((seconds - Hold) / (Gone - Hold));

            bool visible = scale > 0f;
            if (enabled != visible)
                enabled = visible;

            rectTransform.localScale = new Vector3(scale, scale, 1f);
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, seconds * SpinPerSecond);
        }

        protected override void Build(VertexHelper mesh, Rect rect, float feather)
        {
            Vector2 centre = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f - InkWidth;
            if (radius <= 0f)
                return;

            AddStar(mesh, centre, radius + InkWidth, radius * InnerShare + InkWidth, HudSticker.Ink, feather, 0f);
            AddStar(mesh, centre, radius, radius * InnerShare, Face, feather, 0f);
            AddStar(mesh, centre, radius * CoreShare, radius * CoreShare * InnerShare, Core, feather, 0.5f);
        }

        // A disc with a spike over each of its edges, all convex. The disc's edges are hard where the spikes cover them,
        // so the two meet without a seam; only the spikes' outer edges are feathered.
        private static void AddStar(VertexHelper mesh, Vector2 centre, float outer, float inner, Color tint, float feather, float turn)
        {
            for (int i = 0; i < Spikes; ++i)
            {
                Corners[i] = centre + Direction(i + 0.5f + turn) * inner;
                Soft[i] = 0f;
            }

            HudShapes.AddPolygon(mesh, Corners, Spikes, tint, Soft);

            Soft[0] = feather * 0.5f;
            Soft[1] = feather * 0.5f;
            Soft[2] = 0f;
            for (int i = 0; i < Spikes; ++i)
            {
                Corners[0] = centre + Direction(i - 0.5f + turn) * inner;
                Corners[1] = centre + Direction(i + turn) * outer;
                Corners[2] = centre + Direction(i + 0.5f + turn) * inner;
                HudShapes.AddPolygon(mesh, Corners, 3, tint, Soft);
            }
        }

        private static Vector2 Direction(float step)
        {
            float angle = step * (2f * Mathf.PI / Spikes);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        // Past full size and back, like a sticker slapped on.
        private static float PopOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.7f ? Overshoot * (t / 0.7f) : Mathf.Lerp(Overshoot, 1f, (t - 0.7f) / 0.3f);
        }
    }
}
