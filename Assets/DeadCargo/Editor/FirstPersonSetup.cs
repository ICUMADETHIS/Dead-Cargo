using System.Collections.Generic;
using DeadCargo.Controls;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.CameraSystem;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Adds the first-person toggle to the open scene: a controls object under the player whose key (C) switches the ship's
    /// camera between the kit's Interior view and the view the player came from. Safe to run more than once; every change
    /// can be undone.
    /// </summary>
    internal static class FirstPersonSetup
    {
        public const string MenuPath = "Dead Cargo/Setup/Controls - First Person (Open Scene)";

        private const string DialogTitle = "First Person";
        private const string UndoName = "Set Up First Person";
        private const string ControlsName = "FirstPersonControls";
        private const string ViewProperty = "firstPersonView";
        private const string InteriorViewPath = "Assets/SpaceCombatKit/Systems/Basics/CameraSystem/Demo/CameraViews/Interior.asset";
        private const string InteriorViewName = "Interior";

        /// <summary>True when the player has the toggle and it knows the first-person view.</summary>
        public static bool IsInstalled(Scene scene)
        {
            List<FirstPersonInput> found = SceneSetupUtility.FindInScene<FirstPersonInput>(scene);
            if (found.Count == 0)
                return false;

            using (var serialized = new SerializedObject(found[0]))
                return serialized.FindProperty(ViewProperty).objectReferenceValue != null;
        }

        [MenuItem(MenuPath, priority = 214)]
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
            GameAgent player = SceneSetupUtility.FindPlayer(scene);
            if (player == null)
                return Fail("No player game agent was found in the open scene.");

            CameraView interior = FindInteriorView();
            if (interior == null)
                return Fail($"The kit's '{InteriorViewName}' camera view was not found at '{InteriorViewPath}'.");

            Undo.SetCurrentGroupName(UndoName);
            int undoGroup = Undo.GetCurrentGroup();

            List<FirstPersonInput> found = SceneSetupUtility.FindInScene<FirstPersonInput>(scene);
            FirstPersonInput toggle = found.Count > 0 ? found[0] : null;
            if (toggle == null)
            {
                // The kit's game agent collects input scripts from its children when play starts.
                var controls = new GameObject(ControlsName, typeof(FirstPersonInput));
                Undo.RegisterCreatedObjectUndo(controls, "Create First Person Controls");
                Undo.SetTransformParent(controls.transform, player.transform, "Parent First Person Controls");
                controls.transform.localPosition = Vector3.zero;
                toggle = controls.GetComponent<FirstPersonInput>();
            }

            var serialized = new SerializedObject(toggle);
            serialized.FindProperty(ViewProperty).objectReferenceValue = interior;
            serialized.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"First person on C in '{scene.name}'. Save the scene.", toggle);
            return true;
        }

        // The kit's demo view by its path, or any camera view named Interior.
        private static CameraView FindInteriorView()
        {
            var view = AssetDatabase.LoadAssetAtPath<CameraView>(InteriorViewPath);
            if (view != null)
                return view;

            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(CameraView)} {InteriorViewName}"))
            {
                view = AssetDatabase.LoadAssetAtPath<CameraView>(AssetDatabase.GUIDToAssetPath(guid));
                if (view != null && view.name == InteriorViewName)
                    return view;
            }

            return null;
        }

        private static bool Fail(string message)
        {
            EditorUtility.DisplayDialog(DialogTitle, message, "OK");
            return false;
        }
    }
}
