using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Cargo
{
    /// <summary>
    /// Player input for jettisoning cargo. Lives under the player's game agent, like the kit's input scripts. A press ejects
    /// the unit selected in the hold view, or the heaviest stack when none is; holding on dumps the rest of the hold.
    /// </summary>
    public sealed class CargoJettisonInput : VehicleInput
    {
        [Header("Cargo")]
        [SerializeField] private CustomInput jettisonInput = new CustomInput("Cargo", "Jettison, Hold To Dump All", KeyCode.J);

        [Tooltip("Time the key is held before the rest of the hold is dumped, in seconds.")]
        [SerializeField, Min(0.1f)] private float dumpAllHoldTime = 0.6f;

        private CargoJettison jettison;
        private bool dumpArmed;
        private float pressedAt;

        protected override bool Initialize(Vehicle vehicle)
        {
            if (!base.Initialize(vehicle))
                return false;

            // Only runs in vehicles that can jettison.
            jettison = vehicle.GetComponent<CargoJettison>();
            dumpArmed = false;
            return jettison != null;
        }

        protected override void InputUpdate()
        {
            if (jettisonInput.Down())
            {
                jettison.JettisonNext();
                dumpArmed = true;
                pressedAt = Time.time;
            }
            else if (dumpArmed && jettisonInput.Pressed() && Time.time - pressedAt >= dumpAllHoldTime)
            {
                jettison.JettisonAll();
                dumpArmed = false;
            }

            if (jettisonInput.Up())
                dumpArmed = false;
        }
    }
}
