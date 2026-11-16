using System;
using DeadCargo.DevTesting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools.TestRange
{
    /// <summary>A named set of test range objects that can be spawned into a scene together.</summary>
    internal sealed class RangeGroup
    {
        public RangeGroup(string label, params string[] paths)
        {
            Label = label;
            Paths = paths;
        }

        public string Label { get; }

        /// <summary>Hierarchy paths in the template scene, such as "Level/Turrets".</summary>
        public string[] Paths { get; }
    }

    /// <summary>
    /// Keeps the test range blank and spawns its content on demand.
    /// Content comes from a template scene: a saved copy of the fully built range.
    /// Spawning copies the wanted objects out of the template, so references and prefab links survive.
    /// </summary>
    internal static class TestRangeLibrary
    {
        public const string TemplatePath = TemplateFolder + "/TestRangeTemplate.unity";

        private const string TemplateFolder = "Assets/DeadCargo/DevTest/Templates";
        private const string WorkingCopyPath = TemplateFolder + "/TestRangeTemplate.working.unity";

        /// <summary>The player, ship, cameras and game systems: everything that is not optional content.</summary>
        public static readonly RangeGroup PlayerRig = new RangeGroup("Player Rig");

        // Add a line here to make more template content spawnable on its own.
        public static readonly RangeGroup[] OptionalGroups =
        {
            new RangeGroup("Turrets", "Level/Turrets"),
            new RangeGroup("Targets", "Level/StaticAirTargets", "Level/DynamicAirTargets"),
            new RangeGroup("Crates", "Level/Crates"),
            new RangeGroup("Cargo Pickups", SceneSetupUtility.CargoPickupsRoot),
            new RangeGroup("Extraction Point", ExtractionPointSetup.RootName)
        };

        public static bool HasTemplate => AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplatePath) != null;

        public static bool IsSpawned(RangeGroup group, Scene scene)
        {
            if (group == PlayerRig)
                return SceneSetupUtility.FindInScene<DevTestRange>(scene).Count > 0;

            foreach (string path in group.Paths)
            {
                if (Find(scene, path) != null)
                    return true;
            }

            return false;
        }

        /// <summary>Stores a copy of the scene as the template. The scene is saved first.</summary>
        public static bool SaveTemplate(Scene scene)
        {
            if (string.IsNullOrEmpty(scene.path) || !EditorSceneManager.SaveScene(scene))
                return false;

            SceneSetupUtility.EnsureFolder(TemplateFolder);
            AssetDatabase.DeleteAsset(TemplatePath);
            return AssetDatabase.CopyAsset(scene.path, TemplatePath);
        }

        /// <summary>Spawns one group from the template into the target scene.</summary>
        public static void Spawn(RangeGroup group, Scene target)
        {
            SpawnFromTemplate(target, template => Strip(template, group), group != PlayerRig);
        }

        /// <summary>Spawns the whole template into the target scene.</summary>
        public static void SpawnEverything(Scene target)
        {
            SpawnFromTemplate(target, null, false);
        }

        /// <summary>Removes an optional group from the scene. Supports Undo.</summary>
        public static void Remove(RangeGroup group, Scene scene)
        {
            foreach (string path in group.Paths)
            {
                GameObject found = Find(scene, path);
                if (found != null)
                    Undo.DestroyObjectImmediate(found);
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        /// <summary>Removes every object from the scene. Supports Undo.</summary>
        public static void Clear(Scene scene)
        {
            Undo.SetCurrentGroupName("Clear Scene");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (GameObject root in scene.GetRootGameObjects())
                Undo.DestroyObjectImmediate(root);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        // Works on a throwaway copy of the template, because merging consumes the source scene.
        private static void SpawnFromTemplate(Scene target, Action<Scene> strip, bool foldIntoExistingRoots)
        {
            // Clears a working copy left behind by an interrupted spawn.
            AssetDatabase.DeleteAsset(WorkingCopyPath);

            if (!AssetDatabase.CopyAsset(TemplatePath, WorkingCopyPath))
            {
                Debug.LogError("Could not copy the test range template.");
                return;
            }

            try
            {
                Scene working = EditorSceneManager.OpenScene(WorkingCopyPath, OpenSceneMode.Additive);
                strip?.Invoke(working);

                GameObject[] incoming = working.GetRootGameObjects();
                EditorSceneManager.MergeScenes(working, target);

                if (foldIntoExistingRoots)
                    FoldIntoExistingRoots(target, incoming);
            }
            finally
            {
                AssetDatabase.DeleteAsset(WorkingCopyPath);
            }

            SceneManager.SetActiveScene(target);
            EditorSceneManager.MarkSceneDirty(target);
        }

        // Leaves only the given group in the scene. For the player rig, removes every optional group instead.
        private static void Strip(Scene scene, RangeGroup group)
        {
            if (group == PlayerRig)
            {
                foreach (RangeGroup optional in OptionalGroups)
                {
                    foreach (string path in optional.Paths)
                    {
                        GameObject found = Find(scene, path);
                        if (found != null)
                            UnityEngine.Object.DestroyImmediate(found);
                    }
                }

                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (Array.IndexOf(group.Paths, root.name) >= 0)
                    continue;

                // Unrelated roots go whole. Their children are never touched, as prefab instances forbid it.
                string childPrefix = root.name + "/";
                if (!Array.Exists(group.Paths, path => path.StartsWith(childPrefix, StringComparison.Ordinal)))
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    continue;
                }

                // A root holding wanted children is kept as their container, minus its other children.
                for (int i = root.transform.childCount - 1; i >= 0; --i)
                {
                    Transform child = root.transform.GetChild(i);
                    if (Array.IndexOf(group.Paths, childPrefix + child.name) < 0)
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        // Moves spawned children under a same-named root already in the scene, so "Level" is not duplicated.
        private static void FoldIntoExistingRoots(Scene scene, GameObject[] incoming)
        {
            foreach (GameObject spawned in incoming)
            {
                if (spawned == null)
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root == spawned || root.name != spawned.name || Array.IndexOf(incoming, root) >= 0)
                        continue;

                    for (int i = spawned.transform.childCount - 1; i >= 0; --i)
                        spawned.transform.GetChild(i).SetParent(root.transform, true);

                    UnityEngine.Object.DestroyImmediate(spawned);
                    break;
                }
            }
        }

        private static GameObject Find(Scene scene, string path)
        {
            int slash = path.IndexOf('/');
            string rootName = slash < 0 ? path : path.Substring(0, slash);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != rootName)
                    continue;

                if (slash < 0)
                    return root;

                Transform child = root.transform.Find(path.Substring(slash + 1));
                if (child != null)
                    return child.gameObject;
            }

            return null;
        }
    }
}
