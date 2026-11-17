using System;
using System.Collections.Generic;
using System.IO;
using DeadCargo.Sectors.Far;
using SpaceGraphicsToolkit.Cloud;
using SpaceGraphicsToolkit.Landscape;
using SpaceGraphicsToolkit.Ocean;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DeadCargo.EditorTools.Visuals
{
    /// <summary>
    /// Copies Planet Forge's standard planets into Dead Cargo prefabs under <c>Resources/Planets</c>, keeping only what is
    /// seen from orbit, and lists them in the planet catalogue. Each planet's demo scene is opened beside the open scene,
    /// read, and closed without saving, so Planet Forge is never changed. Prefabs that already exist are kept, and so are
    /// the roles and weights set on the catalogue by hand.
    /// </summary>
    internal static class PlanetCatalogBuilder
    {
        public const string CatalogPath = "Assets/DeadCargo/Data/Planets/PlanetCatalog.asset";
        public const string PrefabFolder = "Assets/DeadCargo/Resources/Planets";

        private const string SourceFolder = "Assets/Plugins/CW/Planet Forge/Scenes";
        private const string ResourceFolder = "Planets";
        private const string DialogTitle = "Planet Catalogue";
        private const string SimpleOriginNamespace = "CW.SimpleOriginSystem";

        // Earth-sized, large and lighting variants of a planet carry these in their scene names.
        private static readonly char[] VariantMarks = { '(', '[', '-' };

        // Parts for close views only: under the sea, on the ground.
        private static readonly Type[] CloseViewOnly =
        {
            typeof(SgtOceanDebris), typeof(SgtOceanRays), typeof(SgtOceanSound), typeof(SgtLandscapeStaticSpawner),
            typeof(SgtLandscapeCollider), typeof(Collider)
        };

        /// <summary>The catalogue, or null before it is built.</summary>
        public static PlanetCatalog Find()
        {
            return AssetDatabase.LoadAssetAtPath<PlanetCatalog>(CatalogPath);
        }

        /// <summary>Planet Forge's standard planet scenes: one per planet, without its variants.</summary>
        public static List<string> FindSourceScenes()
        {
            var scenes = new List<string>();
            if (!Directory.Exists(SourceFolder))
                return scenes;

            foreach (string path in Directory.GetFiles(SourceFolder, "*.unity"))
            {
                if (Path.GetFileNameWithoutExtension(path).IndexOfAny(VariantMarks) < 0)
                    scenes.Add(path.Replace('\\', '/'));
            }

            scenes.Sort(string.CompareOrdinal);
            return scenes;
        }

        /// <summary>
        /// Copies every planet that has no prefab yet, then lists every prefab in the catalogue. Returns the catalogue, or
        /// null when it cannot run, after a dialog says why.
        /// </summary>
        public static PlanetCatalog Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(DialogTitle, "Stop Play Mode first.", "OK");
                return null;
            }

            List<string> scenes = FindSourceScenes();
            if (scenes.Count == 0)
            {
                EditorUtility.DisplayDialog(DialogTitle, $"Planet Forge's planet scenes were not found in '{SourceFolder}'.", "OK");
                return null;
            }

            SceneSetupUtility.EnsureFolder(PrefabFolder);
            SceneSetupUtility.EnsureFolder(CatalogPath.Substring(0, CatalogPath.LastIndexOf('/')));

            int copied = 0;
            try
            {
                for (int i = 0; i < scenes.Count; ++i)
                {
                    string planetName = Path.GetFileNameWithoutExtension(scenes[i]);
                    if (EditorUtility.DisplayCancelableProgressBar(DialogTitle, $"Copying {planetName} ({i + 1} of {scenes.Count})", (float)i / scenes.Count))
                        break;

                    if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(planetName)) == null && Copy(scenes[i], PrefabPath(planetName)))
                        ++copied;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            PlanetCatalog catalog = List(scenes);
            Debug.Log($"Planet catalogue: {copied} planet(s) copied, {catalog.Count} listed in '{CatalogPath}'.", catalog);
            return catalog;
        }

        private static string PrefabPath(string planetName)
        {
            return $"{PrefabFolder}/{planetName}.prefab";
        }

        // Every planet with a prefab, in name order. Entries the catalogue already has keep their roles and weight.
        private static PlanetCatalog List(List<string> scenes)
        {
            PlanetCatalog catalog = Find();
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PlanetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var entries = new List<PlanetEntry>();
            foreach (string scene in scenes)
            {
                string planetName = Path.GetFileNameWithoutExtension(scene);
                PlanetEntry described = Describe(planetName);
                if (described == null)
                    continue;

                PlanetEntry existing = catalog.Find(planetName);
                entries.Add(existing == null
                    ? described
                    : new PlanetEntry(planetName, described.ResourcePath, described.Radius, existing.Roles, existing.Weight));
            }

            Undo.RecordObject(catalog, "Build Planet Catalogue");
            catalog.SetEntries(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            return catalog;
        }

        // Ocean worlds make the primary or a distant planet; worlds with air but no sea can be anything; airless worlds
        // are moons or distant planets.
        private static PlanetEntry Describe(string planetName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(planetName));
            SgtSphereLandscape landscape = prefab != null ? prefab.GetComponentInChildren<SgtSphereLandscape>(true) : null;
            if (landscape == null)
                return null;

            bool hasOcean = prefab.GetComponentInChildren<SgtOcean>(true) != null;
            bool hasClouds = prefab.GetComponentInChildren<SgtCloud>(true) != null;
            BodyRoles roles = hasOcean
                ? BodyRoles.Primary | BodyRoles.Distant
                : hasClouds ? BodyRoles.All : BodyRoles.Moon | BodyRoles.Distant;

            float radius = landscape.Radius * landscape.transform.lossyScale.x;
            return new PlanetEntry(planetName, $"{ResourceFolder}/{planetName}", radius, roles, 1f);
        }

        private static bool Copy(string scenePath, string prefabPath)
        {
            Scene source = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                GameObject planet = FindPlanet(source);
                if (planet == null)
                {
                    Debug.LogWarning($"No planet was found in '{scenePath}', so it was skipped.");
                    return false;
                }

                PrefabUtility.SaveAsPrefabAsset(planet, prefabPath, out bool saved);
                if (!saved)
                    return false;
            }
            finally
            {
                EditorSceneManager.CloseScene(source, true);
            }

            Strip(prefabPath);
            return true;
        }

        // The planet is the root holding a landscape. The demo's camera, light and interface are a prefab instance beside it.
        private static GameObject FindPlanet(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(root) && root.GetComponentInChildren<SgtSphereLandscape>(true) != null)
                    return root;
            }

            return null;
        }

        private static void Strip(string prefabPath)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (Type type in CloseViewOnly)
                {
                    foreach (Component component in contents.GetComponentsInChildren(type, true))
                        Object.DestroyImmediate(component);
                }

                foreach (MonoBehaviour behaviour in contents.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null && behaviour.GetType().Namespace == SimpleOriginNamespace)
                        Object.DestroyImmediate(behaviour);
                }

                contents.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                contents.transform.localScale = Vector3.one;
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
