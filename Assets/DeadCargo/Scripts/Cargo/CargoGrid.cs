using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.Cargo
{
    /// <summary>
    /// The space in a ship's hold: a grid of cells, each free or covered by one placed unit. New units are packed first
    /// fit, row by row from the top-left, upright before turned. Plain state with no scene dependency; the hold owns it.
    /// </summary>
    public sealed class CargoGrid
    {
        private const int Free = 0;
        private const int Counted = -1;

        // Placement ID per cell, row by row from the top-left.
        private readonly int[] cells;

        // A copy of the cells for counting how many more units fit without changing the grid.
        private readonly int[] scratch;

        private readonly List<CargoPlacement> placements = new List<CargoPlacement>();
        private int nextId = 1;

        public CargoGrid(int columns, int rows)
        {
            Columns = Mathf.Max(1, columns);
            Rows = Mathf.Max(1, rows);
            cells = new int[Columns * Rows];
            scratch = new int[cells.Length];
        }

        public int Columns { get; }
        public int Rows { get; }
        public int CellCount => cells.Length;

        /// <summary>Cells covered by units.</summary>
        public int UsedCells { get; private set; }

        /// <summary>Every placed unit, in the order it was placed.</summary>
        public IReadOnlyList<CargoPlacement> Placements => placements;

        public bool Contains(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Columns && cell.y < Rows;
        }

        /// <summary>ID of the unit covering the cell, or 0 when the cell is free or outside the grid.</summary>
        public int IdAt(Vector2Int cell)
        {
            return Contains(cell) ? cells[cell.y * Columns + cell.x] : Free;
        }

        public bool TryGet(int id, out CargoPlacement placement)
        {
            int index = IndexOf(id);
            placement = index >= 0 ? placements[index] : default;
            return index >= 0;
        }

        /// <summary>ID of the newest unit of the item, or 0 when there is none.</summary>
        public int LastIdOf(CargoItemDefinition item)
        {
            for (int i = placements.Count - 1; i >= 0; --i)
            {
                if (placements[i].Item == item)
                    return placements[i].Id;
            }

            return Free;
        }

        /// <summary>
        /// True when a footprint fits with its top-left at the cell: inside the grid, on cells that are free or covered by
        /// the unit <paramref name="ignoredId"/>, which lets a unit be checked against a move of its own.
        /// </summary>
        public bool Fits(Vector2Int cell, Vector2Int size, int ignoredId = Free)
        {
            return Fits(cells, cell, size, ignoredId);
        }

        /// <summary>Places a unit in the first free space it fits. False, with nothing placed, when it fits nowhere.</summary>
        public bool TryAdd(CargoItemDefinition item, out CargoPlacement placement)
        {
            if (item == null || !FindSpace(cells, item.Size, out Vector2Int cell, out bool rotated))
            {
                placement = default;
                return false;
            }

            placement = new CargoPlacement(nextId++, item, cell, rotated);
            placements.Add(placement);
            Fill(cells, cell, placement.Size, placement.Id);
            UsedCells += Area(placement.Size);
            return true;
        }

        /// <summary>How many more units of a footprint fit, packed as <see cref="TryAdd"/> packs them, up to <paramref name="limit"/>.</summary>
        public int CountFits(Vector2Int size, int limit)
        {
            if (limit <= 0)
                return 0;

            Array.Copy(cells, scratch, cells.Length);
            int count = 0;
            while (count < limit && FindSpace(scratch, size, out Vector2Int cell, out bool rotated))
            {
                Fill(scratch, cell, CargoPlacement.SizeOf(size, rotated), Counted);
                ++count;
            }

            return count;
        }

        /// <summary>Moves a unit to a new top-left cell, turned or not. False, with nothing moved, when it does not fit there.</summary>
        public bool TryMove(int id, Vector2Int cell, bool rotated)
        {
            int index = IndexOf(id);
            if (index < 0)
                return false;

            CargoPlacement current = placements[index];
            Vector2Int size = CargoPlacement.SizeOf(current.Item.Size, rotated);
            if (!Fits(cells, cell, size, id))
                return false;

            Fill(cells, current.Cell, current.Size, Free);
            Fill(cells, cell, size, id);
            placements[index] = new CargoPlacement(id, current.Item, cell, rotated);
            return true;
        }

        public bool Remove(int id)
        {
            int index = IndexOf(id);
            if (index < 0)
                return false;

            CargoPlacement placement = placements[index];
            Fill(cells, placement.Cell, placement.Size, Free);
            UsedCells -= Area(placement.Size);
            placements.RemoveAt(index);
            return true;
        }

        public void Clear()
        {
            Array.Clear(cells, 0, cells.Length);
            placements.Clear();
            UsedCells = 0;
        }

        private int IndexOf(int id)
        {
            if (id == Free)
                return -1;

            for (int i = 0; i < placements.Count; ++i)
            {
                if (placements[i].Id == id)
                    return i;
            }

            return -1;
        }

        private bool FindSpace(int[] grid, Vector2Int size, out Vector2Int cell, out bool rotated)
        {
            Vector2Int turned = CargoPlacement.SizeOf(size, true);
            bool square = size.x == size.y;
            for (int y = 0; y < Rows; ++y)
            {
                for (int x = 0; x < Columns; ++x)
                {
                    cell = new Vector2Int(x, y);
                    if (Fits(grid, cell, size, Free))
                    {
                        rotated = false;
                        return true;
                    }

                    if (!square && Fits(grid, cell, turned, Free))
                    {
                        rotated = true;
                        return true;
                    }
                }
            }

            cell = default;
            rotated = false;
            return false;
        }

        private bool Fits(int[] grid, Vector2Int cell, Vector2Int size, int ignoredId)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x + size.x > Columns || cell.y + size.y > Rows)
                return false;

            for (int y = cell.y; y < cell.y + size.y; ++y)
            {
                for (int x = cell.x; x < cell.x + size.x; ++x)
                {
                    int occupant = grid[y * Columns + x];
                    if (occupant != Free && occupant != ignoredId)
                        return false;
                }
            }

            return true;
        }

        private void Fill(int[] grid, Vector2Int cell, Vector2Int size, int value)
        {
            for (int y = cell.y; y < cell.y + size.y; ++y)
            {
                for (int x = cell.x; x < cell.x + size.x; ++x)
                    grid[y * Columns + x] = value;
            }
        }

        private static int Area(Vector2Int size)
        {
            return size.x * size.y;
        }
    }
}
