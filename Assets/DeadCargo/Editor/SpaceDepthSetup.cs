using System.Collections.Generic;
using DeadCargo.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Adds the distance fade to the open scene: a Space Depth object in the Environment folder. Safe to run more than
    /// once.
    /// </summary>
    internal static class SpaceDepthSetup
    {
        public const string MenuPath = "Dead Cargo/Setup/Phase 6.9 - Distance Fade (Open Scene)";

        private const string ObjectName = "Space Depth";

        /// <summary>The scene's distance fade, or null when it has none.</summary>
        public static SpaceDepth Find(Scene scene)
        {
            List<SpaceDepth> found = SceneSetupUtility.FindInScene<SpaceDepth>(scene);
            return found.Count > 0 ? found[0] : null;
        }

        [MenuItem(MenuPath, priority = 210)]
        private static void Run()
        {
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>Returns the scene's distance fade, adding it, with undo, when it has none.</summary>
        internal static SpaceDepth Ensure(Scene scene)
        {
            return SceneHierarchy.EnsureObject<SpaceDepth>(scene, ObjectName, SceneHierarchy.Environment);
        }

        /// <summary>Adds the fade to the open scene.</summary>
        internal static bool Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            Undo.SetCurrentGroupName("Add Distance Fade");
            int undoGroup = Undo.GetCurrentGroup();

            SpaceDepth depth = Ensure(scene);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = depth.gameObject;
            Debug.Log($"Distance fade added to '{scene.name}'. It shows in Play Mode; set it on the '{ObjectName}' object.", depth);
            return true;
        }
    }
}
