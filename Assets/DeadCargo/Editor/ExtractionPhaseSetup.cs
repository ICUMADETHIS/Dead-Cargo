using DeadCargo.Extraction;
using DeadCargo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools
{
    /// <summary>Adds the phase 5 extraction systems to the open scene. Safe to run more than once.</summary>
    internal static class ExtractionPhaseSetup
    {
        private const string MenuPath = "Dead Cargo/Setup/Phase 5 - Extraction (Open Scene)";
        private const string DialogTitle = "Phase 5 - Extraction";
        private const string TrackerName = "Run Tracker";
        private const string ResultScreenName = "Run Result Screen";
        private const string HudName = "Extraction HUD";
        private const string ControlsName = "JumpControls";

        [MenuItem(MenuPath, priority = 204)]
        private static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();

            VehicleEngines3D ship = SceneSetupUtility.FindShip(scene);
            if (ship == null || ship.GetComponent<Vehicle>() == null)
            {
                EditorUtility.DisplayDialog(DialogTitle,
                    "Select the player's ship in the Hierarchy, then run this again.", "OK");
                return;
            }

            GameAgent player = SceneSetupUtility.FindPlayer(scene);
            if (player == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, "No player game agent was found in the open scene.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Set Up Phase 5 Extraction");
            int undoGroup = Undo.GetCurrentGroup();

            if (ship.GetComponent<JumpDrive>() == null)
                Undo.AddComponent<JumpDrive>(ship.gameObject);

            EnsureInput(scene, player);
            SceneHierarchy.EnsureObject<RunTracker>(scene, TrackerName, SceneHierarchy.Systems);
            SceneHierarchy.EnsureObject<RunResultScreen>(scene, ResultScreenName, SceneHierarchy.UI, true);
            SceneHierarchy.EnsureObject<ExtractionHud>(scene, HudName, SceneHierarchy.UI, true);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Phase 5 extraction set up on '{ship.name}' in scene '{scene.name}'.", ship);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        // The kit's game agent collects input scripts from its children when play starts.
        private static void EnsureInput(Scene scene, GameAgent player)
        {
            if (SceneSetupUtility.FindInScene<JumpDriveInput>(scene).Count > 0)
                return;

            var controls = new GameObject(ControlsName, typeof(JumpDriveInput));
            Undo.RegisterCreatedObjectUndo(controls, "Create Jump Controls");
            Undo.SetTransformParent(controls.transform, player.transform, "Parent Jump Controls");
            controls.transform.localPosition = Vector3.zero;
        }
    }
}
