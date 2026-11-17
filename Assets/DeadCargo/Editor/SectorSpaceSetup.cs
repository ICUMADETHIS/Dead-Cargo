using System.Collections.Generic;
using DeadCargo.Sectors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Adds sector generation to the open scene: a Sector Space object with its debug view, and a sector
    /// definition asset named after the scene, created with starter regions when it is missing.
    /// Safe to run more than once; an existing definition is never changed.
    /// </summary>
    internal static class SectorSpaceSetup
    {
        public const string MenuPath = "Dead Cargo/Setup/Phase 6.8 - Sector Space (Open Scene)";

        private const string DialogTitle = "Phase 6.8 - Sector Space";
        private const string ObjectName = "Sector Space";
        private const string DefinitionFolder = "Assets/DeadCargo/Data/Sectors";
        private const string DefinitionProperty = "definition";

        private static readonly Vector3 DefaultSize = new Vector3(40000f, 6000f, 40000f);
        private const float DefaultCellSize = 1000f;

        /// <summary>True when the scene has a sector space with a definition.</summary>
        public static bool IsInstalled(Scene scene)
        {
            List<SectorSpace> spaces = SceneSetupUtility.FindInScene<SectorSpace>(scene);
            return spaces.Count > 0 && spaces[0].Definition != null;
        }

        [MenuItem(MenuPath, priority = 207)]
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
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog(DialogTitle, "Save the open scene first. Its sector definition is named after it.", "OK");
                return false;
            }

            Undo.SetCurrentGroupName("Set Up Sector Space");
            int undoGroup = Undo.GetCurrentGroup();

            SectorSpace space = SceneHierarchy.EnsureObject<SectorSpace>(scene, ObjectName, SceneHierarchy.Systems);
            if (space.GetComponent<SectorDebugView>() == null)
                Undo.AddComponent<SectorDebugView>(space.gameObject);

            if (space.Definition == null)
            {
                var serialized = new SerializedObject(space);
                serialized.FindProperty(DefinitionProperty).objectReferenceValue = EnsureDefinition(scene.name);
                serialized.ApplyModifiedProperties();
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = space.gameObject;
            Debug.Log($"Sector space set up in '{scene.name}' with the definition '{space.Definition.name}'.", space.Definition);
            return true;
        }

        private static SectorDefinition EnsureDefinition(string sceneName)
        {
            string path = $"{DefinitionFolder}/{sceneName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<SectorDefinition>(path);
            if (definition != null)
                return definition;

            // The seed comes from the name, so each sector starts out different but repeatable.
            definition = ScriptableObject.CreateInstance<SectorDefinition>();
            definition.Configure(sceneName, unchecked((int)SectorHash.Text(sceneName)), DefaultSize, DefaultCellSize, StarterRegions());

            SceneSetupUtility.EnsureFolder(DefinitionFolder);
            AssetDatabase.CreateAsset(definition, path);
            AssetDatabase.SaveAssets();
            return definition;
        }

        // A layout to start from: open space around the arrival point, a large belt and a wreck field. Meant to be edited.
        // The arrival point never moves. The fields swing to any side of it each run, keeping their distance, and
        // move, turn and resize a little, so every run is a new sector while the arrival point stays clear.
        private static SectorRegion[] StarterRegions()
        {
            return new[]
            {
                new SectorRegion("Arrival Point", SectorRegionKind.Clearing, Vector3.zero, new Vector3(2500f, 2500f, 2500f), 1f, 0.5f),
                new SectorRegion("Main Belt", SectorRegionKind.AsteroidField, new Vector3(5000f, 0f, 8000f), new Vector3(10000f, 1500f, 7000f), 0f,
                    0.8f, 0.5f, new RegionVariation(180f, new Vector3(1000f, 300f, 1000f), 30f, 0.15f)),
                new SectorRegion("Wreck Field", SectorRegionKind.DebrisField, new Vector3(-10000f, 400f, -5000f), new Vector3(5000f, 1200f, 5000f), 0f,
                    0.6f, 0.4f, new RegionVariation(180f, new Vector3(2000f, 300f, 2000f), 180f, 0.25f))
            };
        }
    }
}
