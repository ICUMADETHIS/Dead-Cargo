using DeadCargo.Authority;
using DeadCargo.Heat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools
{
    /// <summary>Adds the phase 4 Authority response to the open scene. Safe to run more than once.</summary>
    internal static class AuthorityPhaseSetup
    {
        private const string MenuPath = "Dead Cargo/Setup/Phase 4 - Authority (Open Scene)";
        private const string DialogTitle = "Phase 4 - Authority";
        private const string ResponseName = "Authority Response";

        [MenuItem(MenuPath, priority = 203)]
        private static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (SceneSetupUtility.FindInScene<HeatTracker>(scene).Count == 0)
            {
                EditorUtility.DisplayDialog(DialogTitle, "The open scene has no heat tracker. Run the Phase 3 setup first.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Set Up Phase 4 Authority");
            int undoGroup = Undo.GetCurrentGroup();

            AuthorityResponse response = EnsureResponse(scene);
            bool hasUnits = EnsureUnitPrefabs(scene, response);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = response.gameObject;

            if (hasUnits)
            {
                Debug.Log($"Phase 4 Authority response set up in scene '{scene.name}'.", response);
            }
            else
            {
                EditorUtility.DisplayDialog(DialogTitle,
                    "No AI spawn point was found to copy the enemy pilot and ship from.\n\n" +
                    "Assign Pilot Prefab and Ship Prefab on the Authority Response object.", "OK");
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static AuthorityResponse EnsureResponse(Scene scene)
        {
            return SceneHierarchy.EnsureObject<AuthorityResponse>(scene, ResponseName, SceneHierarchy.Systems);
        }

        // Reuses the pilot and ship prefabs of an AI spawn point already in the scene.
        private static bool EnsureUnitPrefabs(Scene scene, AuthorityResponse response)
        {
            var serialized = new SerializedObject(response);
            SerializedProperty pilot = serialized.FindProperty("pilotPrefab");
            SerializedProperty ship = serialized.FindProperty("shipPrefab");

            if (pilot.objectReferenceValue != null && ship.objectReferenceValue != null)
                return true;

            foreach (PilotedVehicleSpawn spawn in SceneSetupUtility.FindInScene<PilotedVehicleSpawn>(scene))
            {
                var source = new SerializedObject(spawn);
                Object sourcePilot = source.FindProperty("pilotReference").objectReferenceValue;
                Object sourceShip = source.FindProperty("vehicleReference").objectReferenceValue;

                // Scene objects cannot be instantiated as fresh units, so only prefab assets qualify.
                if (sourcePilot == null || sourceShip == null ||
                    !EditorUtility.IsPersistent(sourcePilot) || !EditorUtility.IsPersistent(sourceShip))
                    continue;

                pilot.objectReferenceValue = sourcePilot;
                ship.objectReferenceValue = sourceShip;
                serialized.ApplyModifiedProperties();
                return true;
            }

            return false;
        }
    }
}
