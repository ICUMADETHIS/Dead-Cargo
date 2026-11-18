using System.Collections.Generic;
using DeadCargo.Cameras;
using DeadCargo.Cargo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;
using VSX.UniversalVehicleCombat.Space;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Phase 6.16 step 1, smooth flight, in the open scene. It adds the chase camera beside the kit's space fighter camera,
    /// with the kit camera's views, vehicle classes and boost view, and switches the kit camera off. It also draws the
    /// player's ship between physics steps and gives it continuous collision, so fast flight does not pass through small rock.
    /// Safe to run more than once; every change can be undone.
    /// </summary>
    internal static class SmoothFlightSetup
    {
        private const string MenuPath = "Dead Cargo/Setup/Flight - Smooth Flight (Open Scene)";
        private const string DialogTitle = "Smooth Flight";
        private const string UndoName = "Set Up Smooth Flight";

        // Settings the chase camera takes from the kit camera it replaces, when it is first added.
        private static readonly string[] CopiedProperties =
        {
            "startingView", "defaultToFirstAvailableView", "specifyCompatibleVehicleClasses", "compatibleVehicleClasses", "boostFieldOfView"
        };

        /// <summary>True when every kit fighter camera has a chase camera, and the player's ship is interpolated with continuous collision.</summary>
        public static bool IsInstalled(Scene scene)
        {
            List<SpaceFighterCameraController> kitCameras = SceneSetupUtility.FindInScene<SpaceFighterCameraController>(scene);
            if (kitCameras.Count == 0)
                return false;

            foreach (SpaceFighterCameraController kitCamera in kitCameras)
            {
                if (kitCamera.GetComponent<ChaseCamera>() == null || kitCamera.enabled)
                    return false;
            }

            foreach (CargoHold hold in SceneSetupUtility.FindInScene<CargoHold>(scene))
            {
                if (hold.TryGetComponent(out Rigidbody body) && !IsSmooth(body))
                    return false;
            }

            return true;
        }

        [MenuItem(MenuPath, priority = 216)]
        private static void Run()
        {
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>Sets up the open scene. Returns false when it cannot, after a dialog explains why.</summary>
        internal static bool Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            List<SpaceFighterCameraController> kitCameras = SceneSetupUtility.FindInScene<SpaceFighterCameraController>(scene);
            if (kitCameras.Count == 0)
                return Fail("The open scene has no kit Space Fighter Camera Controller to replace. It lives on the kit's VehicleCamera.");

            VehicleEngines3D ship = SceneSetupUtility.FindShip(scene);
            if (ship == null)
                return Fail("Select the player's ship in the Hierarchy, then run this again.");

            if (!ship.TryGetComponent(out Rigidbody body))
                return Fail($"'{ship.name}' has no Rigidbody.");

            Undo.SetCurrentGroupName(UndoName);
            int undoGroup = Undo.GetCurrentGroup();

            foreach (SpaceFighterCameraController kitCamera in kitCameras)
                EnsureChaseCamera(kitCamera);

            if (!IsSmooth(body))
            {
                Undo.RecordObject(body, UndoName);
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Smooth flight set up on '{ship.name}' in scene '{scene.name}'.", ship);
            return true;
        }

        private static bool IsSmooth(Rigidbody body)
        {
            return body.interpolation == RigidbodyInterpolation.Interpolate
                && body.collisionDetectionMode == CollisionDetectionMode.ContinuousSpeculative;
        }

        // The chase camera sits beside the kit camera, so the camera finds both when it gathers its controllers.
        private static void EnsureChaseCamera(SpaceFighterCameraController kitCamera)
        {
            ChaseCamera chase = kitCamera.GetComponent<ChaseCamera>();
            if (chase == null)
            {
                chase = Undo.AddComponent<ChaseCamera>(kitCamera.gameObject);
                using (var from = new SerializedObject(kitCamera))
                using (var to = new SerializedObject(chase))
                {
                    foreach (string property in CopiedProperties)
                    {
                        SerializedProperty source = from.FindProperty(property);
                        if (source != null)
                            to.CopyFromSerializedProperty(source);
                    }

                    to.FindProperty("kitCamera").objectReferenceValue = kitCamera;
                    to.ApplyModifiedProperties();
                }
            }

            if (kitCamera.enabled)
            {
                Undo.RecordObject(kitCamera, UndoName);
                kitCamera.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(kitCamera);
            }
        }

        private static bool Fail(string message)
        {
            EditorUtility.DisplayDialog(DialogTitle, message, "OK");
            return false;
        }
    }
}
