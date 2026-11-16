using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// A HUD marker shape drawn as a mesh: an outlined diamond over a target, or an arrow on the screen edge
    /// pointing towards one. The arrow points along +x; the marker layer rotates it.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class HudMarkerGlyph : MaskableGraphic
    {
        // Inner corner of the diamond outline, as a share of the outer size.
        private const float OutlineInner = 0.5f;

        // How far back the arrow's notch sits, as a share of its half-length.
        private const float ArrowNotch = 0.35f;

        private bool isArrow;

        internal void SetArrow(bool arrow)
        {
            if (isArrow == arrow)
                return;

            isArrow = arrow;
            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            useLegacyMeshGeneration = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 centre = rect.center;
            float halfWidth = rect.width * 0.5f;
            float halfHeight = rect.height * 0.5f;
            Color32 tint = color;

            if (isArrow)
            {
                mesh.AddVert(centre + new Vector2(halfWidth, 0f), tint, Vector2.zero);
                mesh.AddVert(centre + new Vector2(-halfWidth, halfHeight), tint, Vector2.zero);
                mesh.AddVert(centre + new Vector2(-halfWidth * ArrowNotch, 0f), tint, Vector2.zero);
                mesh.AddVert(centre + new Vector2(-halfWidth, -halfHeight), tint, Vector2.zero);
                mesh.AddTriangle(0, 1, 2);
                mesh.AddTriangle(0, 2, 3);
                return;
            }

            // Outer corners 0-3 and inner corners 4-7, top, right, bottom, left; the centre stays open.
            for (int ring = 0; ring < 2; ++ring)
            {
                float scale = ring == 0 ? 1f : OutlineInner;
                mesh.AddVert(centre + new Vector2(0f, halfHeight * scale), tint, Vector2.zero);
                mesh.AddVert(centre + new Vector2(halfWidth * scale, 0f), tint, Vector2.zero);
                mesh.AddVert(centre + new Vector2(0f, -halfHeight * scale), tint, Vector2.zero);
                mesh.AddVert(centre + new Vector2(-halfWidth * scale, 0f), tint, Vector2.zero);
            }

            for (int side = 0; side < 4; ++side)
            {
                int next = (side + 1) % 4;
                mesh.AddTriangle(side, next, next + 4);
                mesh.AddTriangle(side, next + 4, side + 4);
            }
        }
    }
}
