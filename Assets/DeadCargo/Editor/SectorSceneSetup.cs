using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Creates and opens Polaris: the first raid location, and the sector runs launch into from the mothership.
    /// It starts as empty space, with the space sky, ambient light and a sun, and nothing else.
    /// An existing scene is opened as it is, never rebuilt.
    /// </summary>
    internal static class SectorSceneSetup
    {
        public const string MenuPath = "Dead Cargo/Setup/Open Polaris Scene";
        public const string SceneName = "Polaris";
        public const string ScenePath = SceneFolder + "/" + SceneName + ".unity";

        // The scene's first name. A project that still has it is renamed on load.
        private const string LegacyScenePath = SceneFolder + "/Deep Space.unity";

        private const string DialogTitle = "Polaris Scene";
        private const string SceneFolder = "Assets/DeadCargo/Scenes";
        private const string SunName = "Directional Light";

        // Flat ambient colours matching the test range, so ships look the same in both without a lighting bake.
        private static readonly Color AmbientSky = new Color(0.186f, 0.224f, 0.243f);
        private static readonly Color AmbientEquator = new Color(0.114f, 0.125f, 0.133f);
        private static readonly Color AmbientGround = new Color(0.047f, 0.043f, 0.035f);

        // The test range's sun.
        private static readonly Color SunColor = new Color(1f, 0.957f, 0.839f);
        private static readonly Vector3 SunAngles = new Vector3(50f, -73f, 0f);
        private const float SunIntensity = 1.2f;

        /// <summary>True when the Polaris scene asset exists.</summary>
        public static bool Exists => AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;

        // Runs after scripts load. Delayed, because assets cannot be moved while the editor is still loading.
        [InitializeOnLoadMethod]
        private static void RenameLegacySceneOnLoad()
        {
            EditorApplication.delayCall += RenameLegacyScene;
        }

        // Moving through the asset database keeps the scene's ID, so the Build Settings and an open copy follow it.
        // The scene flow settings store the path as text, so they are repointed here.
        private static void RenameLegacyScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Exists ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(LegacyScenePath) == null)
                return;

            string error = AssetDatabase.MoveAsset(LegacyScenePath, ScenePath);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"Could not rename '{LegacyScenePath}' to '{ScenePath}'. {error}");
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<DeadCargo.Flow.SceneFlowSettings>(MothershipPhaseSetup.SettingsPath);
            if (settings != null)
            {
                var serialized = new SerializedObject(settings);
                SerializedProperty sector = serialized.FindProperty("sectorScene");
                if (sector.stringValue == LegacyScenePath)
                {
                    sector.stringValue = ScenePath;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Renamed the game sector scene to '{ScenePath}'.", AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
        }

        [MenuItem(MenuPath, priority = 198)]
        private static void Run()
        {
            OpenOrCreate();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>
        /// Opens the Polaris scene, creating it first when it is missing. Offers to save the open scene.
        /// Returns false when the user cancels or the scene cannot be created, after a dialog explains why.
        /// </summary>
        public static bool OpenOrCreate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            if (Exists)
            {
                EditorSceneManager.OpenScene(ScenePath);
                return true;
            }

            Material skybox = SpaceSkySetup.EnsureMaterial(SceneName);
            if (skybox == null)
            {
                EditorUtility.DisplayDialog(DialogTitle, "The space sky shader was not found. Check the Console for shader errors.", "OK");
                return false;
            }

            SceneSetupUtility.EnsureFolder(SceneFolder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Light sun = CreateSun(scene);
            ApplySky(skybox, sun);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                EditorUtility.DisplayDialog(DialogTitle, $"The scene could not be saved to '{ScenePath}'.", "OK");
                return false;
            }

            Debug.Log($"Created the Polaris scene at '{ScenePath}'.", AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
            return true;
        }

        // Render settings belong to the active scene, which is the new one.
        private static void ApplySky(Material skybox, Light sun)
        {
            RenderSettings.skybox = skybox;
            RenderSettings.sun = sun;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
        }

        private static Light CreateSun(Scene scene)
        {
            var gameObject = new GameObject(SunName, typeof(Light));
            if (gameObject.scene != scene)
                SceneManager.MoveGameObjectToScene(gameObject, scene);

            gameObject.transform.rotation = Quaternion.Euler(SunAngles);

            var sun = gameObject.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.None;

            SceneHierarchy.Place(gameObject, SceneHierarchy.Environment);
            return sun;
        }
    }
}
