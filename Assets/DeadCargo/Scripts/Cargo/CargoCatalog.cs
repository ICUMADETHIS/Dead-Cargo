using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.Cargo
{
    /// <summary>
    /// Every cargo item in the game, found by ID. Save data stores item IDs, and the catalog turns them back into items.
    /// Lives in a Resources folder and is kept current by the editor's catalog builder.
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "Dead Cargo/Cargo Catalog")]
    public sealed class CargoCatalog : ScriptableObject
    {
        public const string ResourceName = "CargoCatalog";

        [SerializeField] private CargoItemDefinition[] items = Array.Empty<CargoItemDefinition>();

        private static CargoCatalog instance;
        private Dictionary<string, CargoItemDefinition> lookup;

        /// <summary>The catalog from Resources, or null when it has not been created.</summary>
        public static CargoCatalog Instance
        {
            get
            {
                if (instance == null)
                    instance = Resources.Load<CargoCatalog>(ResourceName);
                return instance;
            }
        }

        public IReadOnlyList<CargoItemDefinition> Items => items;

        /// <summary>Returns the item with the given ID, or null.</summary>
        public CargoItemDefinition Find(string itemId)
        {
            if (lookup == null)
                BuildLookup();

            return itemId != null && lookup.TryGetValue(itemId, out CargoItemDefinition item) ? item : null;
        }

        private void OnEnable()
        {
            lookup = null;
        }

        private void OnValidate()
        {
            lookup = null;
        }

        private void BuildLookup()
        {
            lookup = new Dictionary<string, CargoItemDefinition>(items.Length);
            foreach (CargoItemDefinition item in items)
            {
                if (item != null)
                    lookup[item.Id] = item;
            }
        }
    }
}
