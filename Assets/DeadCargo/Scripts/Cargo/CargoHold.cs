using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.Cargo
{
    /// <summary>
    /// Cargo storage for a vehicle. Ordinary cargo fills a hold limited by mass, and every unit of it also takes a place on
    /// the hold grid (<see cref="Placements"/>), so it needs a free space it fits. Dark matter fills a separate, weightless
    /// slot limited by units and takes no space. Both are summed per item in <see cref="Stacks"/>. Change the hold only
    /// through its methods, which keep the stacks and the grid in step.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CargoHold : MonoBehaviour
    {
        // Absorbs float error so a hold filled to exactly its capacity accepts the last unit.
        private const float MassTolerance = 0.001f;

        [Tooltip("Maximum mass of ordinary cargo, in tonnes.")]
        [SerializeField, Min(0f)] private float massCapacity = 40f;

        [Tooltip("Width of the hold grid, in cells. Every unit of ordinary cargo needs a free space on the grid.")]
        [SerializeField, Min(1)] private int gridColumns = 8;

        [Tooltip("Height of the hold grid, in cells.")]
        [SerializeField, Min(1)] private int gridRows = 5;

        [Tooltip("Maximum dark matter in the sealed slot, in units. Dark matter is weightless and takes no space.")]
        [SerializeField, Min(0)] private int darkMatterCapacity = 200;

        private readonly List<CargoStack> stacks = new List<CargoStack>();
        private CargoGrid grid;

        /// <summary>Raised after the contents change, or a unit moves on the grid.</summary>
        public event Action Changed;

        /// <summary>Raised with the item and unit count when cargo is taken aboard.</summary>
        public event Action<CargoItemDefinition, int> Added;

        /// <summary>Raised when an add request does not fully fit, with what ran out.</summary>
        public event Action<CargoItemDefinition, CargoLimit> CapacityExceeded;

        /// <summary>Every item aboard with its total quantity, in both slots.</summary>
        public IReadOnlyList<CargoStack> Stacks => stacks;

        /// <summary>Every unit of ordinary cargo and its place on the grid, in the order it came aboard.</summary>
        public IReadOnlyList<CargoPlacement> Placements => Grid.Placements;

        public float MassCapacity => massCapacity;
        public int DarkMatterCapacity => darkMatterCapacity;

        public int GridColumns => Grid.Columns;
        public int GridRows => Grid.Rows;
        public int CellCount => Grid.CellCount;

        /// <summary>Grid cells covered by cargo.</summary>
        public int UsedCells => Grid.UsedCells;

        /// <summary>Mass of ordinary cargo, in tonnes. Dark matter adds nothing.</summary>
        public float TotalMass { get; private set; }

        /// <summary>Value of everything aboard, dark matter included, in credits.</summary>
        public int TotalValue { get; private set; }

        /// <summary>Dark matter aboard, in units.</summary>
        public int DarkMatterUnits { get; private set; }

        public float FreeMass => Mathf.Max(0f, massCapacity - TotalMass);
        public bool IsEmpty => stacks.Count == 0;

        /// <summary>Hold mass as a fraction of capacity, 0 to 1.</summary>
        public float LoadFraction => massCapacity > 0f ? Mathf.Clamp01(TotalMass / massCapacity) : 0f;

        // Built on first use, so cargo added before Awake still finds the grid.
        private CargoGrid Grid => grid ??= new CargoGrid(gridColumns, gridRows);

        private void Awake()
        {
            _ = Grid;
        }

        /// <summary>How many more units of an item fit: in the tonnes left and the space left on the grid, or the dark matter slot.</summary>
        public int GetFreeUnits(CargoItemDefinition item)
        {
            if (item == null)
                return 0;

            if (item.Slot == CargoSlot.DarkMatter)
                return Mathf.Max(0, darkMatterCapacity - DarkMatterUnits);

            return Grid.CountFits(item.Size, FreeUnitsByMass(item));
        }

        /// <summary>Adds as many units as fit. Ordinary cargo is packed into the first free spaces on the grid.</summary>
        /// <returns>The number of units accepted.</returns>
        public int Add(CargoItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0)
                return 0;

            int accepted = Mathf.Min(quantity, GetFreeUnits(item));
            if (item.Slot == CargoSlot.Hold)
            {
                // Counted with the same packing, so each accepted unit finds its space.
                int placed = 0;
                while (placed < accepted && Grid.TryAdd(item, out _))
                    ++placed;

                accepted = placed;
            }

            if (accepted > 0)
            {
                AddToStack(item, accepted);
                Recalculate();
                Added?.Invoke(item, accepted);
            }

            if (accepted < quantity)
                CapacityExceeded?.Invoke(item, LimitFor(item));

            return accepted;
        }

        /// <summary>Removes up to the requested number of units, the newest first.</summary>
        /// <returns>The number of units removed.</returns>
        public int Remove(CargoItemDefinition item, int quantity)
        {
            int removed = RemoveWithoutNotify(item, quantity);
            if (removed > 0)
                Recalculate();

            return removed;
        }

        /// <summary>Takes one unit of ordinary cargo out by its place on the grid. False when no unit has that ID.</summary>
        public bool RemovePlacement(int placementId, out CargoPlacement removed)
        {
            if (!Grid.TryGet(placementId, out removed))
                return false;

            Grid.Remove(placementId);
            TakeFromStack(removed.Item, 1);
            Recalculate();
            return true;
        }

        public bool TryGetPlacement(int placementId, out CargoPlacement placement)
        {
            return Grid.TryGet(placementId, out placement);
        }

        /// <summary>The unit covering a grid cell (column, row from the top-left). False when the cell is free or outside.</summary>
        public bool TryGetPlacementAt(Vector2Int cell, out CargoPlacement placement)
        {
            return Grid.TryGet(Grid.IdAt(cell), out placement);
        }

        /// <summary>True when the unit could move to the cell, turned or not, without overlapping another or leaving the grid.</summary>
        public bool CanMove(int placementId, Vector2Int cell, bool rotated)
        {
            return Grid.TryGet(placementId, out CargoPlacement placement)
                && Grid.Fits(cell, CargoPlacement.SizeOf(placement.Item.Size, rotated), placementId);
        }

        /// <summary>Moves a unit on the grid, turned or not. False, with nothing moved, when it does not fit there.</summary>
        public bool Move(int placementId, Vector2Int cell, bool rotated)
        {
            if (!Grid.TryMove(placementId, cell, rotated))
                return false;

            Changed?.Invoke();
            return true;
        }

        /// <summary>Burns dark matter from the slot, for the jump drive.</summary>
        /// <returns>The number of units burned, which is less than requested when the slot runs dry.</returns>
        public int ConsumeDarkMatter(int units)
        {
            int burned = 0;
            for (int i = stacks.Count - 1; i >= 0 && burned < units; --i)
            {
                if (stacks[i].Item.Slot == CargoSlot.DarkMatter)
                    burned += RemoveWithoutNotify(stacks[i].Item, units - burned);
            }

            if (burned > 0)
                Recalculate();

            return burned;
        }

        public void Clear()
        {
            if (IsEmpty)
                return;

            stacks.Clear();
            Grid.Clear();
            Recalculate();
        }

        private int FreeUnitsByMass(CargoItemDefinition item)
        {
            return Mathf.FloorToInt((FreeMass + MassTolerance) / item.UnitMass);
        }

        // Called after adding, so a hold with tonnes to spare ran out of space.
        private CargoLimit LimitFor(CargoItemDefinition item)
        {
            if (item.Slot == CargoSlot.DarkMatter)
                return CargoLimit.DarkMatter;

            return FreeUnitsByMass(item) > 0 ? CargoLimit.Space : CargoLimit.Mass;
        }

        private int RemoveWithoutNotify(CargoItemDefinition item, int quantity)
        {
            int index = IndexOf(item);
            if (index < 0 || quantity <= 0)
                return 0;

            int removed = Mathf.Min(quantity, stacks[index].Quantity);
            if (item.Slot == CargoSlot.Hold)
            {
                for (int i = 0; i < removed; ++i)
                    Grid.Remove(Grid.LastIdOf(item));
            }

            TakeFromStack(item, removed);
            return removed;
        }

        private void AddToStack(CargoItemDefinition item, int quantity)
        {
            int index = IndexOf(item);
            if (index < 0)
                stacks.Add(new CargoStack(item, quantity));
            else
                stacks[index] = new CargoStack(item, stacks[index].Quantity + quantity);
        }

        private void TakeFromStack(CargoItemDefinition item, int quantity)
        {
            int index = IndexOf(item);
            if (index < 0)
                return;

            int remaining = stacks[index].Quantity - quantity;
            if (remaining > 0)
                stacks[index] = new CargoStack(item, remaining);
            else
                stacks.RemoveAt(index);
        }

        private int IndexOf(CargoItemDefinition item)
        {
            for (int i = 0; i < stacks.Count; ++i)
            {
                if (stacks[i].Item == item)
                    return i;
            }

            return -1;
        }

        private void Recalculate()
        {
            float mass = 0f;
            int value = 0;
            int darkMatter = 0;
            for (int i = 0; i < stacks.Count; ++i)
            {
                mass += stacks[i].Mass;
                value += stacks[i].Value;
                if (stacks[i].Item.Slot == CargoSlot.DarkMatter)
                    darkMatter += stacks[i].Quantity;
            }

            TotalMass = mass;
            TotalValue = value;
            DarkMatterUnits = darkMatter;
            Changed?.Invoke();
        }
    }
}
