using DeadCargo.Cargo;
using DeadCargo.Heat;
using DeadCargo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools
{
    /// <summary>Adds the phase 3 heat systems to the open scene. Safe to run more than once.</summary>
    internal static class HeatPhaseSetup
    {
        private const string MenuPath = "Dead Cargo/Setup/Phase 3 - Heat (Open Scene)";
        private const string DialogTitle = "Phase 3 - Heat";
        private const string TrackerName = "Heat Tracker";
        private const string HudName = "Extraction HUD";

        [MenuItem(MenuPath, priority = 202)]
        private static void Run()
        {
            Apply();
        }

        /// <summary>Sets up the open scene. Returns false when it cannot, after a dialog explains why.</summary>
        internal static bool Apply()
        {
            Scene scene = SceneManager.GetActiveScene();

            VehicleEngines3D ship = SceneSetupUtility.FindShip(scene);
            if (ship == null)
            {
                EditorUtility.DisplayDialog(DialogTitle,
                    "Select the player's ship in the Hierarchy, then run this again.", "OK");
                return false;
            }

            Undo.SetCurrentGroupName("Set Up Phase 3 Heat");
            int undoGroup = Undo.GetCurrentGroup();

            SceneHierarchy.EnsureObject<HeatTracker>(scene, TrackerName, SceneHierarchy.Systems);
            EnsureSources(ship.gameObject);
            SceneHierarchy.EnsureObject<ExtractionHud>(scene, HudName, SceneHierarchy.UI, true);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Phase 3 heat set up on '{ship.name}' in scene '{scene.name}'.", ship);
            return true;
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void EnsureSources(GameObject ship)
        {
            if (ship.GetComponent<WeaponHeatSource>() == null)
                Undo.AddComponent<WeaponHeatSource>(ship);

            // Cargo heat needs a hold, which only exists once phase 1 has been set up.
            if (ship.GetComponent<CargoHold>() != null && ship.GetComponent<CargoHeatSource>() == null)
                Undo.AddComponent<CargoHeatSource>(ship);
        }
    }
}
