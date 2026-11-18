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

        [Tooltip("Name on blocks in the hold grid too small for the full name, such as SCRAP. Empty uses the display name.")]
        [SerializeField] private string shortName;

        [Tooltip("Where the item is stored. Dark matter has its own weightless slot that cannot be jettisoned.")]
        [SerializeField] private CargoSlot slot = CargoSlot.Hold;

        [Tooltip("Mass of one unit, in tonnes. Ignored in the dark matter slot, which is weightless.")]
        [SerializeField, Min(0.01f)] private float unitMass = 1f;

        [Tooltip("Sale value of one unit, in credits.")]
        [SerializeField, Min(0)] private int unitValue = 10;

        [Tooltip("Cells one unit takes in the hold grid, width by height, before it is turned. Ignored for dark matter.")]
        [SerializeField] private Vector2Int size = Vector2Int.one;

        [Tooltip("Colour of the item's blocks in the hold grid.")]
        [SerializeField] private Color blockColor = new Color32(239, 188, 112, 255);

        /// <summary>Save-data key. Falls back to the asset name until an ID is assigned.</summary>
        public string Id => string.IsNullOrEmpty(id) ? name : id;

        public string DisplayName => displayName;

        /// <summary>The name for small blocks in the hold grid.</summary>
        public string ShortName => string.IsNullOrEmpty(shortName) ? displayName : shortName;

        public CargoSlot Slot => slot;

        /// <summary>Mass of one unit, in tonnes. Zero for dark matter.</summary>
        public float UnitMass => slot == CargoSlot.DarkMatter ? 0f : unitMass;

        public int UnitValue => unitValue;

        /// <summary>Cells one unit takes in the hold grid, width by height, unturned. At least one by one.</summary>
        public Vector2Int Size => new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));

        public Color BlockColor => blockColor;

        private void OnValidate()
        {
            size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        }
    }
}
