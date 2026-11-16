using UnityEngine;

namespace DeadCargo.Cargo
{
    /// <summary>Static data for one type of cargo.</summary>
    [CreateAssetMenu(fileName = "CargoItem", menuName = "Dead Cargo/Cargo Item")]
    public sealed class CargoItemDefinition : ScriptableObject
    {
        [Tooltip("Stable key in save data. Set from the asset name the first time; never change it once saves exist.")]
        [SerializeField] private string id;

        [SerializeField] private string displayName = "Cargo";

        [Tooltip("Where the item is stored. Dark matter has its own weightless slot that cannot be jettisoned.")]
        [SerializeField] private CargoSlot slot = CargoSlot.Hold;

        [Tooltip("Mass of one unit, in tonnes. Ignored in the dark matter slot, which is weightless.")]
        [SerializeField, Min(0.01f)] private float unitMass = 1f;

        [Tooltip("Sale value of one unit, in credits.")]
        [SerializeField, Min(0)] private int unitValue = 10;

        /// <summary>Save-data key. Falls back to the asset name until an ID is assigned.</summary>
        public string Id => string.IsNullOrEmpty(id) ? name : id;

        public string DisplayName => displayName;
        public CargoSlot Slot => slot;

        /// <summary>Mass of one unit, in tonnes. Zero for dark matter.</summary>
        public float UnitMass => slot == CargoSlot.DarkMatter ? 0f : unitMass;

        public int UnitValue => unitValue;
    }
}
