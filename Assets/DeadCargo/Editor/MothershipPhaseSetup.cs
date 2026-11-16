using System.Collections.Generic;
using DeadCargo.Cargo;
using DeadCargo.Extraction;
using DeadCargo.Flow;
using DeadCargo.Mothership;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Sets up phase 6 with the open scene as the sector: creates the mothership scene if it is missing,
    /// points the scene flow settings at both scenes, adds them to the Build Settings and builds the cargo catalog.
    /// Safe to run more than once.
    /// </summary>
    internal static class MothershipPhaseSetup
    {
        public const string MenuPath = "Dead Cargo/Setup/Phase 6 - Mothership (Open Scene)";
        public const string DefaultMothershipPath = SceneFolder + "/Mothership.unity";
        public const string SettingsPath = ResourcesFolder + "/" + SceneFlowSettings.ResourceName + ".asset";

        private const string DialogTitle = "Phase 6 - Mothership";
        private const string SceneFolder = "Assets/DeadCargo/Scenes";
        private const string ResourcesFolder = "Assets/DeadCargo/Resources";
        private const string HubName = "Mothership Hub";

        private static readonly Color SpaceColor = new Color32(7, 13, 19, 255);

        /// <summary>True when the settings point at a mothership scene that exists and the cargo catalog exists.</summary>
        public static bool IsInstalled
        {
            get
            {
                var settings = AssetDatabase.LoadAssetAtPath<SceneFlowSettings>(SettingsPath);
                return settings != null && !string.IsNullOrEmpty(settings.SectorScene) &&
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(settings.MothershipScene) != null &&
                    AssetDatabase.LoadAssetAtPath<CargoCatalog>(CargoCatalogBuilder.CatalogPath) != null;
            }
        }

        /// <summary>True when the scene flow is set up and launches into the given scene.</summary>
        public static bool IsSector(Scene scene)
        {
            var settings = AssetDatabase.LoadAssetAtPath<SceneFlowSettings>(SettingsPath);
            return IsInstalled && !string.IsNullOrEmpty(scene.path) && settings.SectorScene == scene.path;
        }

        [MenuItem(MenuPath, priority = 205)]
        private static void Run()
        {
            Apply();
        }

        /// <summary>Sets up the open scene as the sector. Returns false when it cannot, after a dialog explains why.</summary>
        internal static bool Apply()
        {
            Scene sector = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(sector.path))
            {
                EditorUtility.DisplayDialog(DialogTitle, "Save the open scene first. It becomes the sector that runs launch into.", "OK");
                return false;
            }

            if (SceneSetupUtility.FindInScene<RunTracker>(sector).Count == 0)
            {
                EditorUtility.DisplayDialog(DialogTitle,
                    "The open scene has no run tracker. Open the sector scene, such as the Dev Test Range, with the Phase 5 setup in it.", "OK");
                return false;
            }

            SceneFlowSettings settings = EnsureSettings(sector.path);
            bool sceneCreated = EnsureMothershipScene(settings.MothershipScene, sector);
            bool buildChanged = EnsureBuildSettings(settings.MothershipScene, sector.path);
            CargoCatalog catalog = CargoCatalogBuilder.EnsureCatalog();

            Selection.activeObject = settings;
            Debug.Log($"Phase 6 mothership set up. Sector: '{sector.path}'. Mothership: '{settings.MothershipScene}'" +
                (sceneCreated ? " (created)" : "") + (buildChanged ? ". Build Settings updated." : ".") +
                $" Cargo catalog lists {catalog.Items.Count} item(s).", settings);
            return true;
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        // The sector is always the open scene. An existing mothership path is kept.
        private static SceneFlowSettings EnsureSettings(string sectorPath)
        {
            var settings = AssetDatabase.LoadAssetAtPath<SceneFlowSettings>(SettingsPath);
            if (settings == null)
            {
                SceneSetupUtility.EnsureFolder(ResourcesFolder);
                settings = ScriptableObject.CreateInstance<SceneFlowSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            var serialized = new SerializedObject(settings);
            SerializedProperty mothership = serialized.FindProperty("mothershipScene");
            if (string.IsNullOrEmpty(mothership.stringValue))
                mothership.stringValue = DefaultMothershipPath;
            serialized.FindProperty("sectorScene").stringValue = sectorPath;
            serialized.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            return settings;
        }

        // Builds the scene additively, so the open scene stays open and active.
        private static bool EnsureMothershipScene(string path, Scene sector)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                return false;

            SceneSetupUtility.EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            Scene mothership = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                CreateCamera(mothership);
                CreateEventSystem(mothership);
                SceneHierarchy.EnsureObject<MothershipHub>(mothership, HubName, SceneHierarchy.UI, true);
                EditorSceneManager.SaveScene(mothership, path);
            }
            finally
            {
                EditorSceneManager.CloseScene(mothership, true);
                SceneManager.SetActiveScene(sector);
            }

            return true;
        }

        private static void CreateCamera(Scene scene)
        {
            var gameObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Main Camera");
            SceneManager.MoveGameObjectToScene(gameObject, scene);

            var camera = gameObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SpaceColor;
            SceneHierarchy.Place(gameObject, SceneHierarchy.Cameras);
        }

        private static void CreateEventSystem(Scene scene)
        {
            var gameObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(gameObject, "Create EventSystem");
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            SceneHierarchy.Place(gameObject, SceneHierarchy.Systems);
        }

        // The mothership goes first, because a build starts in the first scene.
        private static bool EnsureBuildSettings(string mothershipPath, string sectorPath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool changed = EnsureBuildScene(scenes, mothershipPath, 0);
            changed |= EnsureBuildScene(scenes, sectorPath, -1);

            if (changed)
                EditorBuildSettings.scenes = scenes.ToArray();

            return changed;
        }

        // Adds or enables a scene. Moves it to the given index, or appends it when the index is negative.
        private static bool EnsureBuildScene(List<EditorBuildSettingsScene> scenes, string path, int index)
        {
            int current = scenes.FindIndex(scene => scene.path == path);
            if (current >= 0 && scenes[current].enabled && (index < 0 || current == index))
                return false;

            if (current >= 0)
                scenes.RemoveAt(current);

            var entry = new EditorBuildSettingsScene(path, true);
            if (index < 0)
                scenes.Add(entry);
            else
                scenes.Insert(Mathf.Min(index, scenes.Count), entry);

            return true;
        }
    }
}
