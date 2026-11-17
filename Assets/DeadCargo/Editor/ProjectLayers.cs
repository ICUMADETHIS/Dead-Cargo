using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools
{
    /// <summary>Names the project's own layers in the Tags and Layers settings, in one place.</summary>
    internal static class ProjectLayers
    {
        private const string TagManagerPath = "ProjectSettings/TagManager.asset";
        private const string LayersProperty = "layers";

        // Layers 8 to 11 are the kit's, or kept out of its main camera; 31 is used by Unity's previews.
        public const int FirstFree = 12;
        public const int LastFree = 30;

        /// <summary>
        /// The index of the layer named <paramref name="layerName"/>, naming the first free layer when the project has none.
        /// -1 when every free layer is taken. Not undoable: it changes project settings.
        /// </summary>
        public static int Ensure(string layerName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
            if (assets == null || assets.Length == 0)
                return -1;

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty(LayersProperty);
            for (int i = 0; i < layers.arraySize; ++i)
            {
                if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                    return i;
            }

            for (int i = FirstFree; i <= LastFree && i < layers.arraySize; ++i)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue))
                    continue;

                slot.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return i;
            }

            return -1;
        }
    }
}
