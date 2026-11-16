using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Extraction
{
    /// <summary>Player input for the jump drive. Lives under the player's game agent, like the kit's input scripts.</summary>
    public sealed class JumpDriveInput : VehicleInput
    {
        [Header("Jump Drive")]
        [SerializeField] private CustomInput spoolInput = new CustomInput("Jump Drive", "Start Or Cancel Spool", KeyCode.H);

        private JumpDrive drive;

        protected override bool Initialize(Vehicle vehicle)
        {
            if (!base.Initialize(vehicle))
                return false;

            // Only runs in vehicles that can jump.
            drive = vehicle.GetComponent<JumpDrive>();
            return drive != null;
        }

        protected override void InputUpdate()
        {
            if (spoolInput.Down())
                drive.ToggleSpool();
        }
    }
}
