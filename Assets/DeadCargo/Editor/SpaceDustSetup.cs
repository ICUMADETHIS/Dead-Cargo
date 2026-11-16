using System.Collections.Generic;
using DeadCargo.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Adds space dust to the open scene: a Space Dust object in the Environment folder, using the shared dust
    /// material, which is created the first time. Safe to run more than once.
    /// </summary>
    internal static class SpaceDustSetup
    {
        public const string MenuPath = "Dead Cargo/Setup/Phase 6.9 - Space Dust (Open Scene)";
        public const string MaterialPath = "Assets/DeadCargo/Materials/SpaceDust.mat";

        private const string ShaderName = "DeadCargo/Space Dust";
        private const string DialogTitle = "Phase 6.9 - Space Dust";
        private const string ObjectName = "Space Dust";
        private const string MaterialProperty = "material";
        private const string MaterialFolder = "Assets/DeadCargo/Materials";

        /// <summary>The scene's space dust, or null when it has none.</summary>
        public static SpaceDust Find(Scene scene)
        {
            List<SpaceDust> found = SceneSetupUtility.FindInScene<SpaceDust>(scene);
            return found.Count > 0 ? found[0] : null;
        }

        [MenuItem(MenuPath, priority = 209)]
        private static void Run()
        {
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>Adds the dust to the open scene. Returns false when it cannot, after a dialog explains why.</summary>
        internal static bool Apply()
        {
            Material dust = EnsureMaterial();
            if (dust == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, $"The '{ShaderName}' shader was not found. Check the Console for shader errors.", "OK");
                return false;
            }

            Scene scene = SceneManager.GetActiveScene();
            Undo.SetCurrentGroupName("Add Space Dust");
            int undoGroup = Undo.GetCurrentGroup();

            SpaceDust field = SceneHierarchy.EnsureObject<SpaceDust>(scene, ObjectName, SceneHierarchy.Environment);
            var serialized = new SerializedObject(field);
            SerializedProperty property = serialized.FindProperty(MaterialProperty);
            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = dust;
                serialized.ApplyModifiedProperties();
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = field.gameObject;
            Debug.Log($"Space dust added to '{scene.name}'. Its look is set on '{MaterialPath}'.", field);
            return true;
        }

        /// <summary>Returns the shared dust material, creating it when it is missing. Null when the shader is not available.</summary>
        public static Material EnsureMaterial()
        {
            var dust = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (dust != null)
                return dust;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
                return null;

            dust = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(MaterialPath) };
            SceneSetupUtility.EnsureFolder(MaterialFolder);
            AssetDatabase.CreateAsset(dust, MaterialPath);
            return dust;
        }
    }
}
