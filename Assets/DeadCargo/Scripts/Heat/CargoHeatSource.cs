using DeadCargo.Cargo;
using UnityEngine;

namespace DeadCargo.Heat
{
    /// <summary>Raises heat when the ship takes cargo aboard.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CargoHold))]
    public sealed class CargoHeatSource : MonoBehaviour
    {
        [Tooltip("Heat added for each tonne of cargo collected.")]
        [SerializeField, Min(0f)] private float heatPerTonne = 0.5f;

        private CargoHold hold;

        private void Awake()
        {
            hold = GetComponent<CargoHold>();
        }

        private void OnEnable()
        {
            hold.Added += OnCargoAdded;
        }

        private void OnDisable()
        {
            hold.Added -= OnCargoAdded;
        }

        private void OnCargoAdded(CargoItemDefinition item, int quantity)
        {
            if (HeatTracker.Active != null)
                HeatTracker.Active.Add(item.UnitMass * quantity * heatPerTonne);
        }
    }
}
