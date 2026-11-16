using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.Cargo
{
    /// <summary>Mass-limited cargo storage for a vehicle.</summary>
    [DisallowMultipleComponent]
    public sealed class CargoHold : MonoBehaviour
    {
        // Absorbs float error so a hold filled to exactly its capacity accepts the last unit.
        private const float MassTolerance = 0.001f;

        [Tooltip("Maximum cargo mass, in tonnes.")]
        [SerializeField, Min(0f)] private float massCapacity = 40f;

        private readonly List<CargoStack> stacks = new List<CargoStack>();

        /// <summary>Raised after the contents change.</summary>
        public event Action Changed;

        /// <summary>Raised with the item and unit count when cargo is taken aboard.</summary>
        public event Action<CargoItemDefinition, int> Added;

        /// <summary>Raised when an add request does not fully fit.</summary>
        public event Action<CargoItemDefinition> CapacityExceeded;

        public IReadOnlyList<CargoStack> Stacks => stacks;
        public float MassCapacity => massCapacity;
        public float TotalMass { get; private set; }
        public int TotalValue { get; private set; }
        public float FreeMass => Mathf.Max(0f, massCapacity - TotalMass);
        public bool IsEmpty => stacks.Count == 0;

        /// <summary>Hold mass as a fraction of capacity, 0 to 1.</summary>
        public float LoadFraction => massCapacity > 0f ? Mathf.Clamp01(TotalMass / massCapacity) : 0f;

        /// <summary>How many units of an item fit in the remaining space.</summary>
        public int GetFreeUnits(CargoItemDefinition item)
        {
            return item == null ? 0 : Mathf.FloorToInt((FreeMass + MassTolerance) / item.UnitMass);
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
            int index = IndexOf(item);
            if (index < 0 || quantity <= 0)
                return 0;

            int removed = Mathf.Min(quantity, stacks[index].Quantity);
            int remaining = stacks[index].Quantity - removed;
            if (remaining > 0)
                stacks[index] = new CargoStack(item, remaining);
            else
                stacks.RemoveAt(index);

            Recalculate();
            return removed;
        }

        public void Clear()
        {
            if (IsEmpty)
                return;

            stacks.Clear();
            Recalculate();
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
            for (int i = 0; i < stacks.Count; ++i)
            {
                mass += stacks[i].Mass;
                value += stacks[i].Value;
            }

            TotalMass = mass;
            TotalValue = value;
            Changed?.Invoke();
        }
    }
}
