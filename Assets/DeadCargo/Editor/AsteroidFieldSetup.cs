using System.Collections.Generic;
using DeadCargo.EditorTools.Destruction;
using DeadCargo.Sectors;
using DeadCargo.Sectors.Field;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Adds the asteroid field to the open scene's Sector Space: the field component, a field definition named after the
    /// scene, and the instanced rock material, each created when missing, the distance fade that hides the field's
    /// edge, and the field dust. Safe to run more than once; existing assets are never changed, except that the material is made to allow
    /// GPU instancing.
    /// </summary>
    internal static class AsteroidFieldSetup
    {
        public const string MenuPath = "Dead Cargo/Setup/Phase 6.8 - Asteroid Field (Open Scene)";
        public const string MaterialPath = "Assets/DeadCargo/Materials/Comic/ComicFieldRock.mat";

        private const string DialogTitle = "Phase 6.8 - Asteroid Field";
        private const string DefinitionFolder = "Assets/DeadCargo/Data/Sectors";
        private const string MaterialFolder = "Assets/DeadCargo/Materials/Comic";
        private const string DefinitionProperty = "definition";
        private const string MaterialProperty = "material";
        private const string TriplanarKeyword = "_TRIPLANAR";
        private const string MorphKeyword = "_GEOMORPH";
        private const string MorphToggle = "_Geomorph";

        // Field rocks keep still, so each can take its own shade without flicker, unlike moving voxel pieces.
        private const float ShadeVariation = 0.15f;

        /// <summary>The scene's asteroid field, or null when it has none.</summary>
        public static AsteroidField Find(Scene scene)
        {
            List<AsteroidField> found = SceneSetupUtility.FindInScene<AsteroidField>(scene);
            return found.Count > 0 ? found[0] : null;
        }

        /// <summary>
        /// True when the scene has an asteroid field with its definition and an instanced material with the detail morph.
        /// </summary>
        public static bool IsInstalled(Scene scene)
        {
            AsteroidField field = Find(scene);
            return field != null && field.Definition != null && IsReady(field.Material) && FieldDustSetup.IsInstalled(scene);
        }

        private static bool IsReady(Material material)
        {
            return material != null && material.enableInstancing && material.IsKeywordEnabled(MorphKeyword);
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

        /// <summary>Sets up the open scene. Returns false when it cannot, after a dialog explains why.</summary>
        internal static bool Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            List<SectorSpace> spaces = SceneSetupUtility.FindInScene<SectorSpace>(scene);
            if (spaces.Count == 0 || spaces[0].Definition == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, "The scene has no Sector Space yet. Run Set Up Sector from the God Menu Project tab.", "OK");
                return false;
            }

            Material rock = EnsureMaterial();
            if (rock == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, "The comic surface shader was not found. Check the Console for shader errors.", "OK");
                return false;
            }

            Undo.SetCurrentGroupName("Add Asteroid Field");
            int undoGroup = Undo.GetCurrentGroup();

            SectorSpace space = spaces[0];
            AsteroidField field = space.GetComponent<AsteroidField>();
            if (field == null)
                field = Undo.AddComponent<AsteroidField>(space.gameObject);

            var serialized = new SerializedObject(field);
            SerializedProperty definition = serialized.FindProperty(DefinitionProperty);
            if (definition.objectReferenceValue == null)
                definition.objectReferenceValue = EnsureDefinition(scene.name);

            SerializedProperty material = serialized.FindProperty(MaterialProperty);
            if (material.objectReferenceValue == null)
                material.objectReferenceValue = rock;
            serialized.ApplyModifiedProperties();

            if (field.Material != null && !IsReady(field.Material))
            {
                Undo.RecordObject(field.Material, "Prepare Field Rock Material");
                Prepare(field.Material);
                EditorUtility.SetDirty(field.Material);
            }

            // Rocks stop at the draw distance; the distance fade dissolves them there, so they never pop.
            if (SpaceDepthSetup.Find(scene) == null)
                SpaceDepthSetup.Ensure(scene);

            // Dust through the field, so the rocks sit in something.
            if (FieldDustSetup.Ensure(scene) == null)
                Debug.LogWarning("The field dust shader was not found, so the field has no dust. Check the Console for shader errors.");

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = space.gameObject;
            Debug.Log($"Asteroid field added to '{scene.name}'. Its rocks are set on '{field.Definition.name}', their look on '{field.Material.name}'.",
                field.Definition);
            return true;
        }

        private static AsteroidFieldDefinition EnsureDefinition(string sceneName)
        {
            string path = $"{DefinitionFolder}/{sceneName}Field.asset";
            var definition = AssetDatabase.LoadAssetAtPath<AsteroidFieldDefinition>(path);
            if (definition != null)
                return definition;

            definition = ScriptableObject.CreateInstance<AsteroidFieldDefinition>();
            SceneSetupUtility.EnsureFolder(DefinitionFolder);
            AssetDatabase.CreateAsset(definition, path);
            AssetDatabase.SaveAssets();
            return definition;
        }

        // The voxel rock look, so field rocks and carved asteroids match, with instancing and a shade per rock. Without
        // the voxel material, the comic rock with projected texturing, as the field's meshes have no texture coordinates.
        private static Material EnsureMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
                return existing;

            Material source = AssetDatabase.LoadAssetAtPath<Material>(DestructionSetup.VoxelRockMaterialPath);
            if (source == null)
                source = ComicLookSetup.EnsureRockMaterial();
            if (source == null)
                return null;

            var field = new Material(source) { name = "ComicFieldRock" };
            field.SetFloat("_Triplanar", 1f);
            field.EnableKeyword(TriplanarKeyword);
            field.SetFloat("_InstanceVariation", ShadeVariation);
            Prepare(field);

            SceneSetupUtility.EnsureFolder(MaterialFolder);
            AssetDatabase.CreateAsset(field, MaterialPath);
            return field;
        }

        // What the field needs of its material, whatever its look: instancing, and the morph between levels of detail.
        private static void Prepare(Material material)
        {
            material.enableInstancing = true;
            material.SetFloat(MorphToggle, 1f);
            material.EnableKeyword(MorphKeyword);
        }
    }
}
