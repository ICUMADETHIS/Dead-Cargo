using System.Collections.Generic;
using DeadCargo.Destruction;
using DeadCargo.Destruction.Voxels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools.Destruction
{
    /// <summary>
    /// Adds destruction to the open scene: the Destruction System, and the default asteroid types, materials and
    /// rock look it needs. Assets that already exist are never changed, so hand-tuned values are kept. Safe to run
    /// more than once.
    /// </summary>
    internal static class DestructionSetup
    {
        public const string MenuPath = "Dead Cargo/Setup/Phase 6.10 - Destruction (Open Scene)";
        public const string DataFolder = "Assets/DeadCargo/Data/Destruction";
        public const string RockyAsteroidPath = DataFolder + "/RockyAsteroid.asset";
        public const string OreAsteroidPath = DataFolder + "/OreAsteroid.asset";
        public const string VoxelRockMaterialPath = "Assets/DeadCargo/Materials/Comic/ComicVoxelRock.mat";
        public const string TrailRockMaterialPath = "Assets/DeadCargo/Materials/Comic/ComicTrailRock.mat";
        public const string ChipMaterialPath = "Assets/DeadCargo/Materials/Destruction/RockChips.mat";

        private const string MaterialsFolder = DataFolder + "/Materials";
        private const string RockPath = MaterialsFolder + "/Rock.asset";
        private const string IronPath = MaterialsFolder + "/IronOre.asset";
        private const string CrystalPath = MaterialsFolder + "/CobaltCrystal.asset";
        private const string SystemName = "Destruction System";
        private const string DialogTitle = "Phase 6.10 - Destruction";
        private const string ChipShaderName = "Particles/Standard Unlit";

        [MenuItem(MenuPath, priority = 212)]
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
            if (!EnsureAssets(out string problem))
            {
                EditorUtility.DisplayDialog(DialogTitle, problem, "OK");
                return false;
            }

            Scene scene = SceneManager.GetActiveScene();
            Undo.SetCurrentGroupName("Set Up Destruction");
            int undoGroup = Undo.GetCurrentGroup();

            DestructionSystem system = SceneHierarchy.EnsureObject<DestructionSystem>(scene, SystemName, SceneHierarchy.Systems);
            var serialized = new SerializedObject(system);
            SerializedProperty chips = serialized.FindProperty("chipMaterial");
            if (chips.objectReferenceValue == null)
                chips.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(ChipMaterialPath);

            SerializedProperty trail = serialized.FindProperty("trailMaterial");
            if (trail.objectReferenceValue == null)
                trail.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(TrailRockMaterialPath);

            serialized.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Destruction set up in '{scene.name}'. Asteroid types are in '{DataFolder}'.", system);
            return true;
        }

        public static bool IsInstalled(Scene scene)
        {
            return SceneSetupUtility.FindInScene<DestructionSystem>(scene).Count > 0;
        }

        /// <summary>Every asteroid type in the project, by name.</summary>
        public static List<AsteroidDefinition> FindAsteroidTypes()
        {
            var types = new List<AsteroidDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(AsteroidDefinition)))
            {
                var type = AssetDatabase.LoadAssetAtPath<AsteroidDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (type != null)
                    types.Add(type);
            }

            types.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
            return types;
        }

        /// <summary>Creates any default asset that is missing. Returns false with the reason when it cannot.</summary>
        public static bool EnsureAssets(out string problem)
        {
            problem = null;
            Material surface = EnsureVoxelRockMaterial();
            if (surface == null)
            {
                problem = "The 'DeadCargo/Comic Surface' shader was not found. Check the Console for shader errors.";
                return false;
            }

            EnsureChipMaterial();
            EnsureTrailRockMaterial(surface);
            SceneSetupUtility.EnsureFolder(MaterialsFolder);

            VoxelMaterialDefinition rock = EnsureVoxelMaterial(RockPath, "Rock", Color.white, 0f, new Color(0.55f, 0.46f, 0.38f), 2.6f);
            VoxelMaterialDefinition iron = EnsureVoxelMaterial(IronPath, "Iron Ore", new Color(0.92f, 0.45f, 0.16f), 0.85f, new Color(0.78f, 0.4f, 0.18f), 5f);
            VoxelMaterialDefinition crystal = EnsureVoxelMaterial(CrystalPath, "Cobalt Crystal", new Color(0.18f, 0.82f, 0.9f), 1f, new Color(0.3f, 0.85f, 0.95f), 3.2f);

            // Mostly bare rock with a trace of iron; and a rock shot through with iron, with crystal deep inside.
            EnsureAsteroid(RockyAsteroidPath, "Rocky Asteroid", surface, rock, 0.18f, new[] { new Vein(iron, 0.07f, 0.04f, 2f) });
            EnsureAsteroid(OreAsteroidPath, "Ore Asteroid", surface, rock, 0.24f,
                new[] { new Vein(iron, 0.11f, 0.1f, 1f), new Vein(crystal, 0.18f, 0.06f, 4f) });

            AssetDatabase.SaveAssets();
            return true;
        }

        private readonly struct Vein
        {
            public readonly VoxelMaterialDefinition Material;
            public readonly float Frequency;
            public readonly float Thickness;
            public readonly float MinDepth;

            public Vein(VoxelMaterialDefinition material, float frequency, float thickness, float minDepth)
            {
                Material = material;
                Frequency = frequency;
                Thickness = thickness;
                MinDepth = minDepth;
            }
        }

        // The comic rock look with projected texturing for voxel meshes, copied from the rock material so it matches.
        private static Material EnsureVoxelRockMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(VoxelRockMaterialPath);
            if (existing != null)
                return existing;

            Material rock = ComicLookSetup.EnsureRockMaterial();
            if (rock == null)
                return null;

            // Voxel meshes have smooth normals, so the outline follows them; each piece is unique, so no instancing or
            // shade variation, which would flicker on moving pieces.
            var voxel = new Material(rock) { name = "ComicVoxelRock", enableInstancing = false };
            voxel.SetFloat("_Triplanar", 1f);
            voxel.EnableKeyword("_TRIPLANAR");
            voxel.SetFloat("_TriplanarTile", 6f);
            voxel.SetFloat("_TriplanarSharpness", 4f);
            voxel.SetFloat("_OreShadow", 0.35f);
            voxel.SetFloat("_InkGapFill", 0f);
            voxel.SetFloat("_InstanceVariation", 0f);

            SceneSetupUtility.EnsureFolder("Assets/DeadCargo/Materials/Comic");
            AssetDatabase.CreateAsset(voxel, VoxelRockMaterialPath);
            return voxel;
        }

        // The voxel rock look for trail rocks, which are drawn instanced. A material of its own with instancing on keeps
        // that shader variant in builds; a finer texture tile suits rocks a metre across.
        private static void EnsureTrailRockMaterial(Material voxel)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(TrailRockMaterialPath) != null)
                return;

            var trail = new Material(voxel) { name = "ComicTrailRock", enableInstancing = true };
            trail.SetFloat("_TriplanarTile", 2f);
            AssetDatabase.CreateAsset(trail, TrailRockMaterialPath);
        }

        private static void EnsureChipMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(ChipMaterialPath) != null)
                return;

            Shader shader = Shader.Find(ChipShaderName);
            if (shader == null)
                return;

            SceneSetupUtility.EnsureFolder("Assets/DeadCargo/Materials/Destruction");
            AssetDatabase.CreateAsset(new Material(shader) { name = "RockChips" }, ChipMaterialPath);
        }

        private static VoxelMaterialDefinition EnsureVoxelMaterial(string path, string displayName, Color veinColor, float veinStrength,
            Color chipColor, float tonnesPerCubicMetre)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VoxelMaterialDefinition>(path);
            if (existing != null)
                return existing;

            var material = ScriptableObject.CreateInstance<VoxelMaterialDefinition>();
            AssetDatabase.CreateAsset(material, path);
            var serialized = new SerializedObject(material);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("veinColor").colorValue = veinColor;
            serialized.FindProperty("veinStrength").floatValue = veinStrength;
            serialized.FindProperty("chipColor").colorValue = chipColor;
            serialized.FindProperty("tonnesPerCubicMetre").floatValue = tonnesPerCubicMetre;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return material;
        }

        private static void EnsureAsteroid(string path, string displayName, Material surface, VoxelMaterialDefinition rock,
            float bumpiness, Vein[] veins)
        {
            if (AssetDatabase.LoadAssetAtPath<AsteroidDefinition>(path) != null)
                return;

            SceneSetupUtility.EnsureFolder(DataFolder);
            var asteroid = ScriptableObject.CreateInstance<AsteroidDefinition>();
            AssetDatabase.CreateAsset(asteroid, path);
            var serialized = new SerializedObject(asteroid);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("surfaceMaterial").objectReferenceValue = surface;
            serialized.FindProperty("rock").objectReferenceValue = rock;
            serialized.FindProperty("bumpiness").floatValue = bumpiness;

            SerializedProperty list = serialized.FindProperty("veins");
            list.arraySize = veins.Length;
            for (int i = 0; i < veins.Length; ++i)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("material").objectReferenceValue = veins[i].Material;
                element.FindPropertyRelative("frequency").floatValue = veins[i].Frequency;
                element.FindPropertyRelative("thickness").floatValue = veins[i].Thickness;
                element.FindPropertyRelative("minDepth").floatValue = veins[i].MinDepth;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
