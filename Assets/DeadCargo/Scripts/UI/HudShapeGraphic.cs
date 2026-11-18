using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// Base of the comic HUD's mesh graphics. Their edges fade over one screen pixel (<see cref="HudShapes"/>), so the mesh
    /// is rebuilt when the canvas scale changes with the screen's size. Never a raycast target. It requires its own
    /// CanvasRenderer, as Image and Text do: Graphic does not, and without one the mesh is built but never drawn.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal abstract class HudShapeGraphic : MaskableGraphic
    {
        // How wide the soft band along each edge is, in screen pixels.
        private const float FeatherPixels = 1f;

        private float builtScaleFactor = -1f;

        protected override void Awake()
        {
            base.Awake();
            useLegacyMeshGeneration = false;
            raycastTarget = false;
        }

        protected void LateUpdate()
        {
            Canvas owner = canvas;
            if (owner != null && !Mathf.Approximately(owner.rootCanvas.scaleFactor, builtScaleFactor))
                SetVerticesDirty();
        }

        protected sealed override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Build(mesh, GetPixelAdjustedRect(), FeatherPixels / PixelsPerUnit());
        }

        /// <summary>Adds the graphic's shapes to the cleared mesh; <paramref name="feather"/> is a screen pixel in local units.</summary>
        protected abstract void Build(VertexHelper mesh, Rect rect, float feather);

        // Screen pixels per unit of this graphic, which the canvas scaler sets from the screen's size.
        private float PixelsPerUnit()
        {
            Canvas owner = canvas;
            if (owner == null)
            {
                builtScaleFactor = -1f;
                return 1f;
            }

            Canvas root = owner.rootCanvas;
            builtScaleFactor = root.scaleFactor;
            float rootScale = root.transform.lossyScale.x;
            float scale = root.scaleFactor * (rootScale > 0f ? transform.lossyScale.x / rootScale : 1f);
            return scale > 0.01f ? scale : 1f;
        }
    }
}
