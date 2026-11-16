#if UNITY_EDITOR || DEVELOPMENT_BUILD || DDT_ENABLED
using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.Debugging
{
    /// <summary>
    /// A bar per frame for the last few seconds, coloured against the 60 and 30 fps budgets, with a line at each.
    /// A spike shows as one tall red bar, which a frame-rate figure averages away. Rebuilt every frame it shows, into
    /// the UI's pooled vertex lists, so it allocates nothing.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class DebugFrameGraph : Graphic
    {
        // Height of the graph, in milliseconds: three 60 fps budgets.
        private const float TopMs = 50f;
        private const float SixtyFpsMs = 1000f / 60f;
        private const float ThirtyFpsMs = 1000f / 30f;

        private static readonly Color32 GoodColor = new Color32(111, 217, 138, 255);
        private static readonly Color32 WarnColor = new Color32(255, 210, 77, 255);
        private static readonly Color32 BadColor = new Color32(255, 107, 94, 255);
        private static readonly Color32 LineColor = new Color32(255, 255, 255, 70);

        public DebugFrameStats Stats { get; set; }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (Stats == null || Stats.HistoryCount == 0)
                return;

            Rect rect = GetPixelAdjustedRect();
            float barWidth = rect.width / DebugFrameStats.HistoryLength;
            float scale = rect.height / TopMs;
            int count = Stats.HistoryCount;

            // Newest on the right; an empty history fills in from the right as frames arrive.
            float left = rect.xMax - count * barWidth;
            for (int i = 0; i < count; ++i)
            {
                float ms = Stats.HistoryAt(i);
                Color32 color = ms <= SixtyFpsMs ? GoodColor : ms <= ThirtyFpsMs ? WarnColor : BadColor;
                float x = left + i * barWidth;
                Quad(mesh, x, rect.yMin, x + barWidth, rect.yMin + Mathf.Min(ms * scale, rect.height), color);
            }

            Quad(mesh, rect.xMin, rect.yMin + SixtyFpsMs * scale, rect.xMax, rect.yMin + SixtyFpsMs * scale + 1f, LineColor);
            Quad(mesh, rect.xMin, rect.yMin + ThirtyFpsMs * scale, rect.xMax, rect.yMin + ThirtyFpsMs * scale + 1f, LineColor);
        }

        private static void Quad(VertexHelper mesh, float x0, float y0, float x1, float y1, Color32 color)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector3(x0, y0), color, Vector4.zero);
            mesh.AddVert(new Vector3(x0, y1), color, Vector4.zero);
            mesh.AddVert(new Vector3(x1, y1), color, Vector4.zero);
            mesh.AddVert(new Vector3(x1, y0), color, Vector4.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
#endif
