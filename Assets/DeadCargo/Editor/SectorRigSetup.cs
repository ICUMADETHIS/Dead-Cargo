using System;
using System.Collections.Generic;
using DeadCargo.DevTesting;
using DeadCargo.EditorTools.TestRange;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Puts the player in the open game sector, starting aboard the ship. The player rig comes from the test range
    /// template, minus the range's own objects: the on-foot character, the floor, the enemy spawn and the dev bootstrap.
    /// The scene then becomes the sector the mothership launches into. Safe to run more than once.
    /// </summary>
    internal static class SectorRigSetup
    {
        public const string MenuPath = "Dead Cargo/Setup/Spawn Player In Ship (Open Scene)";

        private const string DialogTitle = "Spawn Player In Ship";
        private const string LevelRoot = "Level";
        private const string StartingVehicleProperty = "startingVehicle";

        /// <summary>True when the scene's player starts the run aboard a ship.</summary>
        public static bool IsInstalled(Scene scene)
        {
            GameAgent player = SceneSetupUtility.FindPlayer(scene);
            if (player == null)
                return false;

            var start = new SerializedObject(player).FindProperty(StartingVehicleProperty).objectReferenceValue as Vehicle;
            return start != null && start.GetComponentInChildren<VehicleEngines3D>(true) != null;
        }

        /// <summary>True when the scene is a test range, which keeps its on-foot start.</summary>
        public static bool IsTestRange(Scene scene)
        {
            return SceneSetupUtility.FindInScene<DevTestRange>(scene).Count > 0;
        }

        [MenuItem(MenuPath, priority = 197)]
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
        public static bool Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
                return Fail("Save the open scene first.");

            if (IsTestRange(scene))
                return Fail("The open scene is a test range, where the player starts on foot. Open a game sector, such as Polaris, and run this again.");

            GameAgent player = SceneSetupUtility.FindPlayer(scene);
            bool spawned = player == null;
            if (spawned)
            {
                if (!TestRangeLibrary.HasTemplate)
                    return Fail("There is no test range template to take the player rig from. Save the full test range as the template from the Range tab first.");

                SpawnRig(scene);
                player = SceneSetupUtility.FindPlayer(scene);
            }

            VehicleEngines3D engines = SceneSetupUtility.FindShip(scene);
            Vehicle ship = engines != null ? engines.GetComponentInParent<Vehicle>() : null;
            if (player == null || ship == null)
                return Fail("The scene needs one player and one ship. If it has several ships, select the player's ship in the Hierarchy and run this again.");

            var serializedPlayer = new SerializedObject(player);
            serializedPlayer.FindProperty(StartingVehicleProperty).objectReferenceValue = ship;
            serializedPlayer.ApplyModifiedProperties();

            // A ship the user has already placed stays where it is.
            if (spawned)
            {
                Undo.RecordObject(ship.transform, "Place Ship");
                ship.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            SceneHierarchy.Tidy(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                return Fail("The scene could not be saved.");

            Debug.Log($"'{scene.name}': the player starts aboard '{ship.name}'.", ship);

            // Makes this scene the one the mothership launches into.
            return MothershipPhaseSetup.Apply();
        }

        private static void SpawnRig(Scene scene)
        {
            var existing = new List<GameObject>(scene.GetRootGameObjects());
            TestRangeLibrary.Spawn(TestRangeLibrary.PlayerRig, scene);
            StripRangeObjects(scene);
            MergeFolders(scene, existing);
        }

        // The range's bootstrap names the objects that only make sense in the range.
        private static void StripRangeObjects(Scene scene)
        {
            foreach (DevTestRange range in SceneSetupUtility.FindInScene<DevTestRange>(scene))
            {
                var serialized = new SerializedObject(range);
                Remove(serialized.FindProperty("character").objectReferenceValue as Component);
                Remove(serialized.FindProperty("enemySpawn").objectReferenceValue as Component);
                Undo.DestroyObjectImmediate(range.gameObject);
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == LevelRoot)
                    Undo.DestroyObjectImmediate(root);
            }
        }

        // Part of a prefab instance can only be removed with the whole instance.
        private static void Remove(Component component)
        {
            if (component == null)
                return;

            GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(component.gameObject);
            Undo.DestroyObjectImmediate(instanceRoot != null ? instanceRoot : component.gameObject);
        }

        // The rig arrives with its own folders. The sector keeps its own sky lighting, and takes the rest into its folders.
        private static void MergeFolders(Scene scene, List<GameObject> existing)
        {
            foreach (GameObject incoming in scene.GetRootGameObjects())
            {
                if (existing.Contains(incoming) || Array.IndexOf(SceneHierarchy.Folders, incoming.name) < 0)
                    continue;

                GameObject target = existing.Find(root => root != null && root.name == incoming.name);
                if (target == null)
                    continue;

                if (incoming.name != SceneHierarchy.Environment)
                {
                    for (int i = incoming.transform.childCount - 1; i >= 0; --i)
                        Undo.SetTransformParent(incoming.transform.GetChild(i), target.transform, "Merge " + incoming.name);
                }

                Undo.DestroyObjectImmediate(incoming);
            }
        }

        private static bool Fail(string message)
        {
            EditorUtility.DisplayDialog(DialogTitle, message, "OK");
            return false;
        }
    }
}
