using System;
using System.Collections.Generic;
using DeadCargo.EditorTools.TestRange;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;
using Object = UnityEngine.Object;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Keeps scene hierarchies in one layout. Dead Cargo objects live in folder roots; kit objects stay at the root.
    /// The kit finds a ship's parts through transform.root, so a vehicle or agent inside a folder breaks
    /// hit detection, radar and boarding.
    /// </summary>
    internal static class SceneHierarchy
    {
        /// <summary>Scene services and managers.</summary>
        public const string Systems = "Systems";

        /// <summary>Canvases and screens.</summary>
        public const string UI = "UI";

        /// <summary>Cameras that are not part of the kit's camera rig.</summary>
        public const string Cameras = "Cameras";

        /// <summary>Lights and set dressing.</summary>
        public const string Environment = "Environment";

        /// <summary>Folder roots, in the order they sit at the top of the hierarchy.</summary>
        public static readonly string[] Folders = { Systems, UI, Cameras, Environment };

        public const string TidyMenuPath = "Dead Cargo/Tidy Hierarchy (Open Scene)";

        private const string KitNamespace = "VSX";
        private const string DeadCargoNamespace = "DeadCargo";
        private const string VisualsNamespace = "DeadCargo.Visuals";
        private const string VoxelsNamespace = "DeadCargo.Destruction.Voxels";
        private const string FarNamespace = "DeadCargo.Sectors.Far";

        [MenuItem(TidyMenuPath, priority = 150)]
        private static void TidyOpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            int moved = Tidy(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Tidied '{scene.name}': {moved} object(s) filed into folders.");
        }

        [MenuItem(TidyMenuPath, true)]
        private static bool CanTidyOpenScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>Returns the component in the scene, or creates it on a new object in the given folder. Supports Undo.</summary>
        public static T EnsureObject<T>(Scene scene, string name, string folder, bool isUi = false) where T : Component
        {
            List<T> existing = SceneSetupUtility.FindInScene<T>(scene);
            if (existing.Count > 0)
                return existing[0];

            GameObject gameObject = isUi
                ? new GameObject(name, typeof(RectTransform)) { layer = SceneSetupUtility.UILayer }
                : new GameObject(name);

            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
            if (gameObject.scene != scene)
                SceneManager.MoveGameObjectToScene(gameObject, scene);

            T component = gameObject.AddComponent<T>();
            Place(gameObject, folder);
            return component;
        }

        /// <summary>Moves a root object into a folder of its own scene, creating the folder if needed. Supports Undo.</summary>
        public static void Place(GameObject gameObject, string folder)
        {
            Transform parent = GetFolder(gameObject.scene, folder);
            if (gameObject.transform.parent != parent)
                Undo.SetTransformParent(gameObject.transform, parent, "Move To " + folder);

            SortRoots(gameObject.scene);
        }

        /// <summary>
        /// Files every root object that belongs in a folder and sorts the folders to the top.
        /// Kit objects, vehicles, agents and test range content are left where they are. Supports Undo.
        /// </summary>
        /// <returns>The number of objects moved.</returns>
        public static int Tidy(Scene scene)
        {
            Undo.SetCurrentGroupName("Tidy Hierarchy");
            int undoGroup = Undo.GetCurrentGroup();
            int moved = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                string folder = Classify(root);
                if (folder == null)
                    continue;

                Undo.SetTransformParent(root.transform, GetFolder(scene, folder), "Move To " + folder);
                moved++;
            }

            RemoveEmptyFolders(scene);
            SortRoots(scene);
            Undo.CollapseUndoOperations(undoGroup);
            return moved;
        }

        private static Transform GetFolder(Scene scene, string folder)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == folder && IsFolder(root))
                    return root.transform;
            }

            var created = new GameObject(folder);
            Undo.RegisterCreatedObjectUndo(created, "Create " + folder + " Folder");
            if (created.scene != scene)
                SceneManager.MoveGameObjectToScene(created, scene);

            return created.transform;
        }

        // Returns the folder a root object belongs in, or null to leave it at the root.
        private static string Classify(GameObject root)
        {
            if (IsFolder(root) || MustStayAtRoot(root))
                return null;

            if (root.TryGetComponent(out Canvas _))
                return UI;

            // Dead Cargo's visual effects, far space and placed asteroids are set dressing, not systems.
            if (HasComponentFrom(root, VisualsNamespace) || HasComponentFrom(root, FarNamespace) || HasComponentFrom(root, VoxelsNamespace))
                return Environment;
            if (root.TryGetComponent(out EventSystem _) || HasComponentFrom(root, DeadCargoNamespace))
                return Systems;
            if (root.TryGetComponent(out Camera _))
                return Cameras;
            if (root.TryGetComponent(out Light _) || root.TryGetComponent(out ReflectionProbe _))
                return Environment;

            return null;
        }

        private static bool MustStayAtRoot(GameObject root)
        {
            return root.GetComponentInChildren<Vehicle>(true) != null ||
                root.GetComponentInChildren<GameAgent>(true) != null ||
                PrefabUtility.IsAnyPrefabInstanceRoot(root) ||
                HasComponentFrom(root, KitNamespace) ||
                IsTestRangeContent(root);
        }

        // The Range tab spawns and removes its content by root name.
        private static bool IsTestRangeContent(GameObject root)
        {
            foreach (RangeGroup group in TestRangeLibrary.OptionalGroups)
            {
                foreach (string path in group.Paths)
                {
                    int slash = path.IndexOf('/');
                    if ((slash < 0 ? path : path.Substring(0, slash)) == root.name)
                        return true;
                }
            }

            return false;
        }

        private static bool HasComponentFrom(GameObject gameObject, string namespacePrefix)
        {
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                string space = component != null ? component.GetType().Namespace : null;
                if (space != null && space.StartsWith(namespacePrefix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        // A folder is an empty object at the origin whose name matches one of the folder names.
        private static bool IsFolder(GameObject gameObject)
        {
            return Array.IndexOf(Folders, gameObject.name) >= 0 &&
                gameObject.GetComponents<Component>().Length == 1 &&
                gameObject.transform.localPosition == Vector3.zero &&
                gameObject.transform.localRotation == Quaternion.identity &&
                gameObject.transform.localScale == Vector3.one;
        }

        private static void RemoveEmptyFolders(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (IsFolder(root) && root.transform.childCount == 0)
                    Undo.DestroyObjectImmediate(root);
            }
        }

        // Folders go to the top in their fixed order; everything else keeps its relative order below them.
        private static void SortRoots(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            int index = 0;
            foreach (string folder in Folders)
            {
                foreach (GameObject root in roots)
                {
                    if (root.name != folder || !IsFolder(root))
                        continue;

                    Undo.SetSiblingIndex(root.transform, index++, "Sort Folders");
                    break;
                }
            }
        }
    }
}
