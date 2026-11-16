using System.IO;
using DeadCargo.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Scene flow between the mothership and the sector: open the scenes in the editor, or travel between them in play.</summary>
    internal sealed class MothershipTab : GodMenuTab
    {
        private static readonly string[] PlayLabels = { "Go To Mothership", "Launch Sector" };
        private static readonly string[] EditLabels = { "Open Mothership", "Open Sector" };
        private static readonly string[] SettingsLabels = { "Select Settings" };

        public override string Title => "Mothership";
        public override int Order => 45;

        public override void OnGUI()
        {
            SceneFlowSettings settings = SceneFlow.Settings;
            if (settings == null)
            {
                EditorGUILayout.HelpBox("No scene flow yet. Run the Phase 6 setup from the Project tab with the sector scene open.", MessageType.Warning);
                return;
            }

            DrawSceneFlow(settings);
        }

        private static void DrawSceneFlow(SceneFlowSettings settings)
        {
            using (var section = new GodMenuGUI.SectionScope("Scene Flow"))
            {
                if (!section.Expanded)
                    return;

                GodMenuGUI.Field("Mothership", SceneLabel(settings.MothershipScene));
                GodMenuGUI.Field("Sector", SceneLabel(settings.SectorScene));

                if (GodMenuContext.IsPlaying)
                    DrawTravel();
                else
                    DrawOpen(settings);

                if (GodMenuGUI.ButtonRow(SettingsLabels) == 0)
                {
                    Selection.activeObject = settings;
                    EditorGUIUtility.PingObject(settings);
                }
            }
        }

        private static void DrawTravel()
        {
            bool idle = !SceneFlow.IsLoading;
            GodMenuGUI.Field("Loading", idle ? "No" : "Yes");

            bool[] enabled = { idle && SceneFlow.HasMothership && !SceneFlow.InMothership, idle && SceneFlow.HasSector };
            switch (GodMenuGUI.ButtonRow(PlayLabels, enabled))
            {
                case 0:
                    SceneFlow.LoadMothership();
                    break;
                case 1:
                    SceneFlow.LaunchSector();
                    break;
            }
        }

        private static void DrawOpen(SceneFlowSettings settings)
        {
            bool[] enabled = { SceneExists(settings.MothershipScene), SceneExists(settings.SectorScene) };
            switch (GodMenuGUI.ButtonRow(EditLabels, enabled))
            {
                case 0:
                    OpenScene(settings.MothershipScene);
                    break;
                case 1:
                    OpenScene(settings.SectorScene);
                    break;
            }
        }

        // Opening a scene rebuilds the editor layout mid-draw, so the current GUI pass is abandoned.
        private static void OpenScene(string path)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path);

            GUIUtility.ExitGUI();
        }

        private static bool SceneExists(string path)
        {
            return !string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
        }

        private static string SceneLabel(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "Not set";

            return SceneExists(path) ? Path.GetFileNameWithoutExtension(path) : "Missing";
        }
    }
}
