using UnityEngine;

namespace DeadCargo.Cargo
{
    /// <summary>Static data for one type of cargo.</summary>
    [CreateAssetMenu(fileName = "CargoItem", menuName = "Dead Cargo/Cargo Item")]
    public sealed class CargoItemDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Cargo";

        [Tooltip("Mass of one unit, in tonnes.")]
        [SerializeField, Min(0.01f)] private float unitMass = 1f;

        [Tooltip("Sale value of one unit, in credits.")]
        [SerializeField, Min(0)] private int unitValue = 10;

        public string DisplayName => displayName;
        public float UnitMass => unitMass;
        public int UnitValue => unitValue;
    }
}
