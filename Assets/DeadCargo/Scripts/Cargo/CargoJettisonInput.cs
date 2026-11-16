using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Cargo
{
    /// <summary>Player input for jettisoning cargo. Lives under the player's game agent, like the kit's input scripts.</summary>
    public sealed class CargoJettisonInput : VehicleInput
    {
        [Header("Cargo")]
        [SerializeField] private CustomInput jettisonInput = new CustomInput("Cargo", "Jettison Heaviest Stack", KeyCode.J);

        private CargoJettison jettison;

        protected override bool Initialize(Vehicle vehicle)
        {
            if (!base.Initialize(vehicle))
                return false;

            // Only runs in vehicles that can jettison.
            jettison = vehicle.GetComponent<CargoJettison>();
            return jettison != null;
        }

        protected override void InputUpdate()
        {
            if (jettisonInput.Down())
                jettison.JettisonHeaviest();
        }
    }
}
