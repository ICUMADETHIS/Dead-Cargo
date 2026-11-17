using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Flight
{
    /// <summary>Player input for the cruise drive. Lives under the player's game agent, like the kit's input scripts.</summary>
    public sealed class CruiseDriveInput : VehicleInput
    {
        [Header("Cruise Drive")]
        [SerializeField] private CustomInput driveInput = new CustomInput("Cruise Drive", "Engage, Cancel Or Drop Out", KeyCode.B);

        private CruiseDrive drive;

        protected override bool Initialize(Vehicle vehicle)
        {
            if (!base.Initialize(vehicle))
                return false;

            // Only runs in vehicles with a cruise drive.
            drive = vehicle.GetComponent<CruiseDrive>();
            return drive != null;
        }

        protected override void InputUpdate()
        {
            if (driveInput.Down())
                drive.Toggle();
        }
    }
}
