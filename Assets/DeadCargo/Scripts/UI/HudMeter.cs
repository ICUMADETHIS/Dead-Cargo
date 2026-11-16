using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class HudMeter : MaskableGraphic
    {
        private static readonly Color TrackColor = new Color32(47, 69, 76, 255);
        private const float SegmentGap = 3f;

        private int segmentCount = 12;
        private float[] boundaries;
        private float progress;
        private Color fillColor = Color.white;

        internal void SetBoundaries(float[] values)
        {
            boundaries = values;
            SetVerticesDirty();
        }

        internal void SetProgress(float value, Color tint)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(progress, value) && fillColor == tint)
                return;

            progress = value;
            fillColor = tint;
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
            int count = boundaries != null ? boundaries.Length - 1 : segmentCount;
            for (int i = 0; i < count; i++)
            {
                float start = boundaries != null ? boundaries[i] : (float)i / count;
                float end = boundaries != null ? boundaries[i + 1] : (float)(i + 1) / count;
                float left = rect.xMin + rect.width * start + (i == 0 ? 0f : SegmentGap * .5f);
                float right = rect.xMin + rect.width * end - (i == count - 1 ? 0f : SegmentGap * .5f);
                if (right <= left)
                    continue;

                AddQuad(mesh, left, right, rect.yMin, rect.yMax, TrackColor);

                float filledTo = Mathf.Min(right, rect.xMin + rect.width * progress);
                if (filledTo > left)
                    AddQuad(mesh, left, filledTo, rect.yMin, rect.yMax, fillColor);
            }
        }

        private static void AddQuad(VertexHelper mesh, float left, float right, float bottom, float top, Color tint)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(new Vector2(left, bottom), tint, Vector2.zero);
            mesh.AddVert(new Vector2(left, top), tint, Vector2.zero);
            mesh.AddVert(new Vector2(right, top), tint, Vector2.zero);
            mesh.AddVert(new Vector2(right, bottom), tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}