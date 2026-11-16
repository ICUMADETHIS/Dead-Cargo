using DeadCargo.Flow;
using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools
{
    /// <summary>Shows the scene paths on <see cref="SceneFlowSettings"/> as scene pickers.</summary>
    [CustomEditor(typeof(SceneFlowSettings))]
    internal sealed class SceneFlowSettingsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawScene(serializedObject.FindProperty("mothershipScene"), "Mothership Scene");
            DrawScene(serializedObject.FindProperty("sectorScene"), "Sector Scene");
            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawScene(SerializedProperty path, string label)
        {
            var current = AssetDatabase.LoadAssetAtPath<SceneAsset>(path.stringValue);
            var picked = (SceneAsset)EditorGUILayout.ObjectField(new GUIContent(label, path.tooltip), current, typeof(SceneAsset), false);

            if (picked != current)
                path.stringValue = picked != null ? AssetDatabase.GetAssetPath(picked) : string.Empty;
            else if (current == null && !string.IsNullOrEmpty(path.stringValue))
                EditorGUILayout.HelpBox($"No scene at '{path.stringValue}'.", MessageType.Warning);
        }
    }
}
