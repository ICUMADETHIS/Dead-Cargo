using UnityEngine;

namespace DeadCargo.Sectors
{
    /// <summary>
    /// Divides a sector into equal cubic cells, centred on the sector's origin. Positions are in sector space, in metres.
    /// Cell (0, 0, 0) is the corner cell at the lowest x, y and z.
    /// </summary>
    public readonly struct SectorGrid
    {
        /// <summary>Edge length of a cell, in metres.</summary>
        public readonly float CellSize;

        /// <summary>Number of cells along each axis.</summary>
        public readonly int CountX;
        public readonly int CountY;
        public readonly int CountZ;

        /// <summary>The grid's lowest corner, in sector space.</summary>
        public readonly Vector3 Min;

        /// <summary>Builds the smallest grid of whole cells that covers a sector of the given size.</summary>
        public SectorGrid(Vector3 sectorSize, float cellSize)
        {
            CellSize = Mathf.Max(1f, cellSize);
            CountX = Mathf.Max(1, Mathf.CeilToInt(sectorSize.x / CellSize));
            CountY = Mathf.Max(1, Mathf.CeilToInt(sectorSize.y / CellSize));
            CountZ = Mathf.Max(1, Mathf.CeilToInt(sectorSize.z / CellSize));
            Min = new Vector3(CountX, CountY, CountZ) * (-0.5f * CellSize);
        }

        /// <summary>Size of the whole grid, in metres.</summary>
        public Vector3 Size => new Vector3(CountX, CountY, CountZ) * CellSize;

        public long CellCount => (long)CountX * CountY * CountZ;

        /// <summary>The cell holding a position. The result may lie outside the grid; check it with <see cref="Contains"/>.</summary>
        public CellCoord CellAt(Vector3 sectorPosition)
        {
            Vector3 local = (sectorPosition - Min) / CellSize;
            return new CellCoord(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y), Mathf.FloorToInt(local.z));
        }

        public bool Contains(CellCoord cell)
        {
            return cell.X >= 0 && cell.X < CountX && cell.Y >= 0 && cell.Y < CountY && cell.Z >= 0 && cell.Z < CountZ;
        }

        /// <summary>The lowest corner of a cell, in sector space.</summary>
        public Vector3 CellMin(CellCoord cell)
        {
            return Min + new Vector3(cell.X, cell.Y, cell.Z) * CellSize;
        }

        /// <summary>The centre of a cell, in sector space.</summary>
        public Vector3 CellCentre(CellCoord cell)
        {
            return Min + new Vector3(cell.X + 0.5f, cell.Y + 0.5f, cell.Z + 0.5f) * CellSize;
        }
    }
}
