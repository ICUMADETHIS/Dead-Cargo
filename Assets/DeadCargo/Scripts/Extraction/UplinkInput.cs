using DeadCargo.Cargo;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Extraction
{
    /// <summary>
    /// Player input that starts the uplink of the nearest extraction point in range.
    /// Lives under the player's game agent, like the kit's input scripts.
    /// </summary>
    public sealed class UplinkInput : VehicleInput
    {
        [Header("Extraction Point")]
        [SerializeField] private CustomInput uplinkInput = new CustomInput("Extraction", "Start Extraction Point Uplink", KeyCode.U);

        private Transform ship;

        protected override bool Initialize(Vehicle vehicle)
        {
            if (!base.Initialize(vehicle))
                return false;

            // Only a ship with a hold can call in an extraction.
            if (vehicle.GetComponent<CargoHold>() == null)
                return false;

            ship = vehicle.transform;
            return true;
        }

        protected override void InputUpdate()
        {
            if (!uplinkInput.Down())
                return;

            ExtractionPoint point = ExtractionPoint.FindNearest(ship.position);
            if (point != null && point.IsInActivationRange(ship.position))
                point.StartUplink();
        }
    }
}
