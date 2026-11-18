using UnityEngine;

namespace DeadCargo.Cargo
{
    /// <summary>
    /// One unit of cargo placed in a hold grid: its item, the cell of its top-left corner (column, row, row 0 at the top),
    /// and whether it is turned a quarter, which swaps its width and height. The ID stays the same while it is moved.
    /// </summary>
    public readonly struct CargoPlacement
    {
        public CargoPlacement(int id, CargoItemDefinition item, Vector2Int cell, bool rotated)
        {
            Id = id;
            Item = item;
            Cell = cell;
            Rotated = rotated;
        }

        /// <summary>Unique in its hold for the life of the unit. Never zero.</summary>
        public int Id { get; }

        public CargoItemDefinition Item { get; }
        public Vector2Int Cell { get; }
        public bool Rotated { get; }

        /// <summary>Cells covered, width by height, after turning.</summary>
        public Vector2Int Size => SizeOf(Item.Size, Rotated);

        /// <summary>A footprint turned a quarter when <paramref name="rotated"/>.</summary>
        public static Vector2Int SizeOf(Vector2Int size, bool rotated)
        {
            return rotated ? new Vector2Int(size.y, size.x) : size;
        }

        public bool Covers(Vector2Int cell)
        {
            Vector2Int size = Size;
            return cell.x >= Cell.x && cell.y >= Cell.y && cell.x < Cell.x + size.x && cell.y < Cell.y + size.y;
        }
    }
}
