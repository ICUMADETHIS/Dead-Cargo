using System;
using System.Collections.Generic;
using DeadCargo.Cargo;
using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Keeps the cargo catalog listing every cargo item, and gives each item a stable save ID.
    /// Runs on its own whenever cargo items are imported, moved or deleted, once the catalog exists.
    /// </summary>
    internal sealed class CargoCatalogBuilder : AssetPostprocessor
    {
        public const string CatalogPath = "Assets/DeadCargo/Resources/" + CargoCatalog.ResourceName + ".asset";

        private const string ResourcesFolder = "Assets/DeadCargo/Resources";
        private const string AssetExtension = ".asset";

        private static bool rebuildQueued;

        /// <summary>Creates the catalog if it is missing, then rebuilds it. Returns the catalog.</summary>
        public static CargoCatalog EnsureCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CargoCatalog>(CatalogPath);
            if (catalog == null)
            {
                SceneSetupUtility.EnsureFolder(ResourcesFolder);
                catalog = ScriptableObject.CreateInstance<CargoCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            Rebuild(catalog);
            return catalog;
        }

        // Deleted assets can no longer be inspected, so any deleted .asset queues a rebuild.
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (rebuildQueued || AssetDatabase.LoadAssetAtPath<CargoCatalog>(CatalogPath) == null)
                return;

            if (ContainsCargoItem(imported) || ContainsCargoItem(moved) || ContainsAsset(deleted))
            {
                // Deferred, because assets should not be changed while an import is still running.
                rebuildQueued = true;
                EditorApplication.delayCall += () =>
                {
                    rebuildQueued = false;
                    var catalog = AssetDatabase.LoadAssetAtPath<CargoCatalog>(CatalogPath);
                    if (catalog != null)
                        Rebuild(catalog);
                };
            }
        }

        private static void Rebuild(CargoCatalog catalog)
        {
            var items = new List<CargoItemDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(CargoItemDefinition)))
            {
                var item = AssetDatabase.LoadAssetAtPath<CargoItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null)
                    continue;

                AssignId(item);
                items.Add(item);
            }

            items.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            WarnOnDuplicateIds(items);

            var serialized = new SerializedObject(catalog);
            SerializedProperty list = serialized.FindProperty("items");
            if (!Matches(list, items))
            {
                list.arraySize = items.Count;
                for (int i = 0; i < items.Count; ++i)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];

                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(catalog);
            }
        }

        // The asset name becomes the ID the first time, so existing items keep the key they already saved under.
        private static void AssignId(CargoItemDefinition item)
        {
            var serialized = new SerializedObject(item);
            SerializedProperty id = serialized.FindProperty("id");
            if (!string.IsNullOrEmpty(id.stringValue))
                return;

            id.stringValue = item.name;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(item);
        }

        private static void WarnOnDuplicateIds(List<CargoItemDefinition> items)
        {
            for (int i = 1; i < items.Count; ++i)
            {
                if (items[i].Id == items[i - 1].Id)
                    Debug.LogWarning($"Cargo items '{items[i - 1].name}' and '{items[i].name}' share the ID '{items[i].Id}'. Saves cannot tell them apart.", items[i]);
            }
        }

        private static bool Matches(SerializedProperty list, List<CargoItemDefinition> items)
        {
            if (list.arraySize != items.Count)
                return false;

            for (int i = 0; i < items.Count; ++i)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue != items[i])
                    return false;
            }

            return true;
        }

        private static bool ContainsCargoItem(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path.EndsWith(AssetExtension, StringComparison.OrdinalIgnoreCase) &&
                    AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(CargoItemDefinition))
                    return true;
            }

            return false;
        }

        private static bool ContainsAsset(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path.EndsWith(AssetExtension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
