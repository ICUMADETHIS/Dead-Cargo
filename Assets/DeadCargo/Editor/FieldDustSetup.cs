using System.Collections.Generic;
using DeadCargo.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Adds field dust to the open scene: a Field Dust object in the Environment folder, using the shared field dust
    /// material, which is created the first time. Safe to run more than once.
    /// </summary>
    internal static class FieldDustSetup
    {
        public const string MenuPath = "Dead Cargo/Setup/Phase 6.8 - Field Dust (Open Scene)";
        public const string MaterialPath = "Assets/DeadCargo/Materials/FieldDust.mat";

        private const string ShaderName = "DeadCargo/Field Dust";
        private const string DialogTitle = "Phase 6.8 - Field Dust";
        private const string ObjectName = "Field Dust";
        private const string MaterialProperty = "material";
        private const string MaterialFolder = "Assets/DeadCargo/Materials";

        /// <summary>The scene's field dust, or null when it has none.</summary>
        public static FieldDust Find(Scene scene)
        {
            List<FieldDust> found = SceneSetupUtility.FindInScene<FieldDust>(scene);
            return found.Count > 0 ? found[0] : null;
        }

        /// <summary>True when the scene has field dust with a material.</summary>
        public static bool IsInstalled(Scene scene)
        {
            FieldDust dust = Find(scene);
            return dust != null && dust.Material != null;
        }

        [MenuItem(MenuPath, priority = 208)]
        private static void Run()
        {
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>
        /// Returns the scene's field dust, adding it, with undo, when it has none, and giving it the shared material
        /// when it has none. Null when the shader is not available.
        /// </summary>
        internal static FieldDust Ensure(Scene scene)
        {
            Material look = EnsureMaterial();
            if (look == null)
                return null;

            FieldDust dust = SceneHierarchy.EnsureObject<FieldDust>(scene, ObjectName, SceneHierarchy.Environment);
            var serialized = new SerializedObject(dust);
            SerializedProperty property = serialized.FindProperty(MaterialProperty);
            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = look;
                serialized.ApplyModifiedProperties();
            }

            return dust;
        }

        /// <summary>Adds the dust to the open scene. Returns false when it cannot, after a dialog explains why.</summary>
        internal static bool Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            Undo.SetCurrentGroupName("Add Field Dust");
            int undoGroup = Undo.GetCurrentGroup();

            FieldDust dust = Ensure(scene);
            if (dust == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, $"The '{ShaderName}' shader was not found. Check the Console for shader errors.", "OK");
                return false;
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = dust.gameObject;
            Debug.Log($"Field dust added to '{scene.name}'. It shows in Play Mode, inside asteroid fields; its look is set on '{MaterialPath}'.", dust);
            return true;
        }

        /// <summary>Returns the shared field dust material, creating it when it is missing. Null when the shader is not available.</summary>
        public static Material EnsureMaterial()
        {
            var look = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (look != null)
                return look;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
                return null;

            look = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(MaterialPath) };
            SceneSetupUtility.EnsureFolder(MaterialFolder);
            AssetDatabase.CreateAsset(look, MaterialPath);
            return look;
        }
    }
}
