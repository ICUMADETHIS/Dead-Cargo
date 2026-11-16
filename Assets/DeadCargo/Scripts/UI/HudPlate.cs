using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class HudPlate : MaskableGraphic
    {
        private static readonly Color TopColor = new Color32(18, 31, 38, 246);
        private static readonly Color BottomColor = new Color32(9, 17, 24, 244);
        private static readonly Color EdgeColor = new Color32(117, 150, 153, 126);

        private Color accent = new Color32(232, 185, 111, 255);

        internal void SetAccent(Color value)
        {
            if (accent == value)
                return;

            accent = value;
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
            float clip = Mathf.Min(12f, rect.height * 0.28f);
            for (int i = 0; i < 6; i++)
            {
                Vector2 point = Corner(rect, clip, i);
                AddVertex(mesh, point, Color.Lerp(BottomColor, TopColor, Mathf.InverseLerp(rect.yMin, rect.yMax, point.y)));
            }

            for (int i = 1; i < 5; i++)
                mesh.AddTriangle(0, i, i + 1);

            for (int i = 0; i < 6; i++)
                DrawEdge(mesh, Corner(rect, clip, i), Corner(rect, clip, (i + 1) % 6), EdgeColor, 1f);

            Vector2 left = new Vector2(rect.xMin + 1f, rect.yMax - clip - 2f);
            DrawEdge(mesh, left, left + Vector2.down * Mathf.Min(24f, rect.height * 0.55f), accent, 2.5f);
            Vector2 bottom = new Vector2(rect.xMax - clip - 2f, rect.yMin + 1f);
            DrawEdge(mesh, bottom + Vector2.left * 23f, bottom, new Color(accent.r, accent.g, accent.b, .66f), 1f);
        }

        private static Vector2 Corner(Rect rect, float clip, int index)
        {
            switch (index)
            {
                case 0: return new Vector2(rect.xMin, rect.yMin);
                case 1: return new Vector2(rect.xMin, rect.yMax - clip);
                case 2: return new Vector2(rect.xMin + clip, rect.yMax);
                case 3: return new Vector2(rect.xMax, rect.yMax);
                case 4: return new Vector2(rect.xMax, rect.yMin + clip);
                default: return new Vector2(rect.xMax - clip, rect.yMin);
            }
        }

        private static void DrawEdge(VertexHelper mesh, Vector2 from, Vector2 to, Color tint, float width)
        {
            Vector2 normal = Vector2.Perpendicular((to - from).normalized) * (width * .5f);
            int offset = mesh.currentVertCount;
            AddVertex(mesh, from - normal, tint);
            AddVertex(mesh, from + normal, tint);
            AddVertex(mesh, to + normal, tint);
            AddVertex(mesh, to - normal, tint);
            mesh.AddTriangle(offset, offset + 1, offset + 2);
            mesh.AddTriangle(offset, offset + 2, offset + 3);
        }

        private static void AddVertex(VertexHelper mesh, Vector2 point, Color tint)
        {
            mesh.AddVert(point, tint, Vector2.zero);
        }
    }
}