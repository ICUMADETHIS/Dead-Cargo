using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.Cargo
{
    /// <summary>
    /// Cargo storage for a vehicle. Ordinary cargo fills a mass-limited hold. Dark matter fills a separate,
    /// weightless slot limited by units. Both are listed in <see cref="Stacks"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CargoHold : MonoBehaviour
    {
        // Absorbs float error so a hold filled to exactly its capacity accepts the last unit.
        private const float MassTolerance = 0.001f;

        [Tooltip("Maximum mass of ordinary cargo, in tonnes.")]
        [SerializeField, Min(0f)] private float massCapacity = 40f;

        [Tooltip("Maximum dark matter in the sealed slot, in units. Dark matter is weightless.")]
        [SerializeField, Min(0)] private int darkMatterCapacity = 200;

        private readonly List<CargoStack> stacks = new List<CargoStack>();

        /// <summary>Raised after the contents change.</summary>
        public event Action Changed;

        /// <summary>Raised with the item and unit count when cargo is taken aboard.</summary>
        public event Action<CargoItemDefinition, int> Added;

        /// <summary>Raised when an add request does not fully fit.</summary>
        public event Action<CargoItemDefinition> CapacityExceeded;

        /// <summary>Every stack aboard, in both slots.</summary>
        public IReadOnlyList<CargoStack> Stacks => stacks;

        public float MassCapacity => massCapacity;
        public int DarkMatterCapacity => darkMatterCapacity;

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

        /// <summary>How many units of an item fit in the space left in its slot.</summary>
        public int GetFreeUnits(CargoItemDefinition item)
        {
            if (item == null)
                return 0;

            if (item.Slot == CargoSlot.DarkMatter)
                return Mathf.Max(0, darkMatterCapacity - DarkMatterUnits);

            return Mathf.FloorToInt((FreeMass + MassTolerance) / item.UnitMass);
        }

        /// <summary>Adds as many units as fit.</summary>
        /// <returns>The number of units accepted.</returns>
        public int Add(CargoItemDefinition item, int quantity)
        {
            if (item == null || quantity <= 0)
                return 0;

            int accepted = Mathf.Min(quantity, GetFreeUnits(item));
            if (accepted > 0)
            {
                int index = IndexOf(item);
                if (index < 0)
                    stacks.Add(new CargoStack(item, accepted));
                else
                    stacks[index] = new CargoStack(item, stacks[index].Quantity + accepted);

                Recalculate();
                Added?.Invoke(item, accepted);
            }

            if (accepted < quantity)
                CapacityExceeded?.Invoke(item);

            return accepted;
        }

        /// <summary>Removes up to the requested number of units.</summary>
        /// <returns>The number of units removed.</returns>
        public int Remove(CargoItemDefinition item, int quantity)
        {
            int removed = RemoveWithoutNotify(item, quantity);
            if (removed > 0)
                Recalculate();

            return removed;
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
            Recalculate();
        }

        private int RemoveWithoutNotify(CargoItemDefinition item, int quantity)
        {
            int index = IndexOf(item);
            if (index < 0 || quantity <= 0)
                return 0;

            int removed = Mathf.Min(quantity, stacks[index].Quantity);
            int remaining = stacks[index].Quantity - removed;
            if (remaining > 0)
                stacks[index] = new CargoStack(item, remaining);
            else
                stacks.RemoveAt(index);

            return removed;
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
