using UnityEngine;
using VSX.CameraSystem;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Controls
{
    /// <summary>
    /// Player input that switches the ship's camera between first person, the view from the cockpit, and the view the
    /// player came from, on one key. The kit's own view keys keep working beside it. Lives under the player's game agent,
    /// like the kit's input scripts, and runs only in vehicles that offer the first-person view.
    /// </summary>
    public sealed class FirstPersonInput : VehicleInput
    {
        [Header("First Person")]
        [Tooltip("The camera view from the cockpit: the kit's Interior view. Set by Set Up Sector.")]
        [SerializeField] private CameraView firstPersonView;

        [SerializeField] private CustomInput toggleInput = new CustomInput("Camera Controls", "Toggle first person", KeyCode.C);

        private CameraTarget cameraTarget;
        private CameraView returnView;

        protected override bool Initialize(Vehicle vehicle)
        {
            if (!base.Initialize(vehicle) || firstPersonView == null)
                return false;

            cameraTarget = vehicle.GetComponentInChildren<CameraTarget>();
            returnView = null;
            return cameraTarget != null && Offers(cameraTarget, firstPersonView);
        }

        protected override void InputUpdate()
        {
            if (!toggleInput.Down())
                return;

            CameraEntity view = cameraTarget.CameraEntity;
            if (view == null)
                return;

            CameraView current = view.CurrentView;
            if (current != firstPersonView)
            {
                returnView = current;
                view.SetView(firstPersonView);
            }
            else if (returnView != null && returnView != firstPersonView)
            {
                view.SetView(returnView);
            }
            else
            {
                view.CycleCameraView(true);
            }
        }

        private static bool Offers(CameraTarget target, CameraView cameraView)
        {
            foreach (CameraViewTarget viewTarget in target.CameraViewTargets)
            {
                if (viewTarget != null && viewTarget.CameraView == cameraView)
                    return true;
            }

            return false;
        }
    }
}
