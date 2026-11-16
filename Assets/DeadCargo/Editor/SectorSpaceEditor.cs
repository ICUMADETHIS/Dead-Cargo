using DeadCargo.EditorTools.Diagnostics;
using DeadCargo.Sectors;
using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Inspector and Scene view tools for a sector space. The sector definition is edited in place below the
    /// component, and each region can be moved and resized with handles in the Scene view. Every edit supports Undo.
    /// </summary>
    [CustomEditor(typeof(SectorSpace))]
    internal sealed class SectorSpaceEditor : Editor
    {
        private const float HandleSize = 0.06f;
        private const float MinRadius = 10f;

        private static readonly Color AsteroidColor = new Color(0.94f, 0.74f, 0.44f);
        private static readonly Color DebrisColor = new Color(0.7f, 0.53f, 1f);
        private static readonly Color ClearingColor = new Color(0.49f, 0.89f, 0.62f);

        private static readonly Vector3[] Axes = { Vector3.right, Vector3.up, Vector3.forward };

        private Editor definitionEditor;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SectorDefinition definition = ((SectorSpace)target).Definition;
            if (definition == null)
            {
                EditorGUILayout.HelpBox("Assign a sector definition, or run Dead Cargo > Setup > Phase 6.8 - Sector Space to create one.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(definition.name, EditorStyles.boldLabel);
            CreateCachedEditor(definition, null, ref definitionEditor);
            definitionEditor.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Regions can be dragged and resized in the Scene view while this object is selected.", MessageType.None);
            if (GUILayout.Button("Run Sector Self-Test"))
                SectorDiagnostics.Run();
        }

        private void OnDisable()
        {
            if (definitionEditor != null)
                DestroyImmediate(definitionEditor);
        }

        // Handles edit the definition asset directly, so the layout is shared by every scene that uses it.
        private void OnSceneGUI()
        {
            SectorDefinition definition = ((SectorSpace)target).Definition;
            if (definition == null || Application.isPlaying)
                return;

            var serialized = new SerializedObject(definition);
            SerializedProperty regions = serialized.FindProperty("regions");

            for (int i = 0; i < regions.arraySize; ++i)
            {
                SerializedProperty region = regions.GetArrayElementAtIndex(i);
                SerializedProperty centre = region.FindPropertyRelative("centre");
                SerializedProperty radii = region.FindPropertyRelative("radii");
                var kind = (SectorRegionKind)region.FindPropertyRelative("kind").enumValueIndex;

                Handles.color = KindColor(kind);
                Handles.Label(centre.vector3Value, region.FindPropertyRelative("name").stringValue, EditorStyles.whiteBoldLabel);

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(centre.vector3Value, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                    centre.vector3Value = moved;

                radii.vector3Value = RadiusHandles(centre.vector3Value, radii.vector3Value);
            }

            serialized.ApplyModifiedProperties();
        }

        // A dot at each end of each axis. Dragging either end changes that axis's radius.
        private static Vector3 RadiusHandles(Vector3 centre, Vector3 radii)
        {
            for (int axis = 0; axis < 3; ++axis)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 direction = Axes[axis] * side;
                    Vector3 position = centre + direction * radii[axis];
                    float size = HandleUtility.GetHandleSize(position) * HandleSize;

                    EditorGUI.BeginChangeCheck();
                    Vector3 dragged = Handles.Slider(position, direction, size, Handles.DotHandleCap, 0f);
                    if (EditorGUI.EndChangeCheck())
                        radii[axis] = Mathf.Max(MinRadius, Vector3.Dot(dragged - centre, direction));
                }
            }

            return radii;
        }

        private static Color KindColor(SectorRegionKind kind)
        {
            switch (kind)
            {
                case SectorRegionKind.DebrisField:
                    return DebrisColor;
                case SectorRegionKind.Clearing:
                    return ClearingColor;
                default:
                    return AsteroidColor;
            }
        }
    }
}
