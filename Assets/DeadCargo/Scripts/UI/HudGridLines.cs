using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>The floor of the hold grid: a dark face, a hairline between every cell and an ink border, filling its rect.</summary>
    internal sealed class HudGridLines : HudShapeGraphic
    {
        private const float LineWidth = 1.5f;
        private const float BorderWidth = 3f;

        private static readonly Color Floor = new Color32(13, 15, 19, 255);
        private static readonly Color Line = new Color32(42, 47, 56, 255);
        private static readonly Vector2[] Corners = new Vector2[4];

        private int columns = 1;
        private int rows = 1;

        internal void SetCells(int columnCount, int rowCount)
        {
            columnCount = Mathf.Max(1, columnCount);
            rowCount = Mathf.Max(1, rowCount);
            if (columnCount == columns && rowCount == rows)
                return;

            columns = columnCount;
            rows = rowCount;
            SetVerticesDirty();
        }

        protected override void Build(VertexHelper mesh, Rect rect, float feather)
        {
            Corners[0] = new Vector2(rect.xMin, rect.yMin);
            Corners[1] = new Vector2(rect.xMin, rect.yMax);
            Corners[2] = new Vector2(rect.xMax, rect.yMax);
            Corners[3] = new Vector2(rect.xMax, rect.yMin);
            HudShapes.AddPolygon(mesh, Corners, 4, Floor, feather);

            float cellWidth = rect.width / columns;
            float cellHeight = rect.height / rows;
            for (int x = 1; x < columns; ++x)
            {
                float along = rect.xMin + x * cellWidth;
                HudShapes.AddStroke(mesh, new Vector2(along, rect.yMin), new Vector2(along, rect.yMax), LineWidth, Line, feather);
            }

            for (int y = 1; y < rows; ++y)
            {
                float along = rect.yMin + y * cellHeight;
                HudShapes.AddStroke(mesh, new Vector2(rect.xMin, along), new Vector2(rect.xMax, along), LineWidth, Line, feather);
            }

            Color ink = HudSticker.Ink;
            HudShapes.AddStroke(mesh, Corners[0], Corners[1], BorderWidth, ink, feather);
            HudShapes.AddStroke(mesh, Corners[1], Corners[2], BorderWidth, ink, feather);
            HudShapes.AddStroke(mesh, Corners[2], Corners[3], BorderWidth, ink, feather);
            HudShapes.AddStroke(mesh, Corners[3], Corners[0], BorderWidth, ink, feather);
        }
    }
}
