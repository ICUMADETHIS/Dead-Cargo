using System.Reflection;
using DeadCargo.EditorTools.Visuals;
using DeadCargo.Sectors;
using DeadCargo.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Gives the open scene Dead Cargo's procedural space sky. Each scene gets its own sky material, named after it,
    /// with star and nebula seeds taken from the name, and a Space Sky object whose randomizer gives every load a
    /// new nebula. Safe to run more than once; an existing material is never changed, only re-baked when out of date.
    /// </summary>
    internal static class SpaceSkySetup
    {
        public const string MenuPath = "Dead Cargo/Setup/Phase 6.9 - Space Sky (Open Scene)";

        private const string DialogTitle = "Phase 6.9 - Space Sky";
        private const string MaterialFolder = "Assets/DeadCargo/Materials/Sky";
        private const string UndoName = "Apply Space Sky";
        private const string ObjectName = "Space Sky";

        /// <summary>True when the scene has its space sky and the randomizer. Render settings are only readable for the active scene.</summary>
        public static bool IsInstalled(Scene scene)
        {
            return HasSkyMaterial(scene) && SceneSetupUtility.FindInScene<SpaceSkyRandomizer>(scene).Count > 0;
        }

        /// <summary>True when the scene's skybox is a Space Sky material.</summary>
        public static bool HasSkyMaterial(Scene scene)
        {
            return scene == SceneManager.GetActiveScene() && IsSpaceSky(RenderSettings.skybox);
        }

        public static bool IsSpaceSky(Material material)
        {
            return material != null && material.shader != null && material.shader.name == SpaceSkyProperties.ShaderName;
        }

        public static string MaterialPath(string sceneName)
        {
            return $"{MaterialFolder}/{sceneName}Sky.mat";
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

        /// <summary>Sets the open scene's skybox to its space sky. Returns false when it cannot, after a dialog explains why.</summary>
        internal static bool Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog(DialogTitle, "Save the open scene first. Its sky material is named after it.", "OK");
                return false;
            }

            Material sky = EnsureMaterial(scene.name);
            if (sky == null)
            {
                EditorUtility.DisplayDialog(DialogTitle,
                    $"The '{SpaceSkyProperties.ShaderName}' shader was not found. Check the Console for shader errors.", "OK");
                return false;
            }

            Undo.SetCurrentGroupName(UndoName);
            int undoGroup = Undo.GetCurrentGroup();
            if (RenderSettings.skybox != sky)
            {
                RecordRenderSettings();
                RenderSettings.skybox = sky;
                DynamicGI.UpdateEnvironment();
            }

            SceneHierarchy.EnsureObject<SpaceSkyRandomizer>(scene, ObjectName, SceneHierarchy.Environment);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);

            Selection.activeObject = sky;
            Debug.Log($"'{scene.name}' uses the space sky '{sky.name}', with a new nebula each load. Edit the look on the selected material.", sky);
            return true;
        }

        /// <summary>
        /// Returns the sky material for a scene, creating it when it is missing and baking its nebula when the map is
        /// missing or out of date. Null when the shader is not available.
        /// </summary>
        public static Material EnsureMaterial(string sceneName)
        {
            string path = MaterialPath(sceneName);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                Shader shader = Shader.Find(SpaceSkyProperties.ShaderName);
                if (shader == null)
                    return null;

                sky = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };

                // Seeds come from the name, so each sector's sky starts out different but repeatable.
                uint hash = SectorHash.Text(sceneName);
                sky.SetFloat(SpaceSkyProperties.StarSeed, hash % (SpaceSkyProperties.MaxSeed + 1u));
                sky.SetFloat(SpaceSkyProperties.NebulaSeed, SectorHash.Combine(hash, 1u) % (SpaceSkyProperties.MaxSeed + 1u));

                SceneSetupUtility.EnsureFolder(MaterialFolder);
                AssetDatabase.CreateAsset(sky, path);
            }

            if (!SpaceSkyBaker.IsCurrent(sky))
            {
                long milliseconds = SpaceSkyBaker.Bake(sky);
                Debug.Log($"Baked the nebula for '{sky.name}' in {milliseconds} ms.", sky);
            }

            return sky;
        }

        // Render settings live on a hidden object in the scene that Unity exposes no public handle to. It is found by
        // reflection so the change can be undone; if Unity ever renames it, the change still applies, without undo.
        internal static void RecordRenderSettings(string undoName = UndoName)
        {
            MethodInfo getter = typeof(RenderSettings).GetMethod("GetRenderSettings", BindingFlags.Static | BindingFlags.NonPublic);
            if (getter != null && getter.Invoke(null, null) is Object settings)
                Undo.RecordObject(settings, undoName);
        }
    }
}
