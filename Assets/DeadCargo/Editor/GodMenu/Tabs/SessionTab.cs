using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Play mode, time and scene controls.</summary>
    internal sealed class SessionTab : GodMenuTab
    {
        private static readonly float[] TimeScalePresets = { 0.25f, 0.5f, 1f, 2f, 4f };
        private static readonly string[] TimeScaleLabels = { "0.25x", "0.5x", "1x", "2x", "4x" };
        private static readonly string[] SceneLabels = { "Restart Scene", "Save Scene" };

        public override string Title => "Session";
        public override int Order => 0;

        public override void OnGUI()
        {
            DrawPlayMode();
            DrawTime();
            DrawScene();
        }

        private static void DrawPlayMode()
        {
            using (var section = new GodMenuGUI.SectionScope("Play Mode"))
            {
                if (!section.Expanded)
                    return;

                bool playing = EditorApplication.isPlaying;
                string[] labels = { playing ? "Stop" : "Play", EditorApplication.isPaused ? "Resume" : "Pause", "Step" };
                bool[] enabled = { true, playing, playing };

                switch (GodMenuGUI.ButtonRow(labels, enabled))
                {
                    case 0:
                        EditorApplication.isPlaying = !playing;
                        break;
                    case 1:
                        EditorApplication.isPaused = !EditorApplication.isPaused;
                        break;
                    case 2:
                        EditorApplication.Step();
                        break;
                }
            }
        }

        private static void DrawTime()
        {
            using (var section = new GodMenuGUI.SectionScope("Time"))
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (!section.Expanded)
                    return;

                float scale = EditorGUILayout.Slider("Time Scale", Time.timeScale, 0f, 4f);
                if (!Mathf.Approximately(scale, Time.timeScale))
                    Time.timeScale = scale;

                int preset = GodMenuGUI.ButtonRow(TimeScaleLabels);
                if (preset >= 0)
                    Time.timeScale = TimeScalePresets[preset];
            }
        }

        private static void DrawScene()
        {
            using (var section = new GodMenuGUI.SectionScope("Scene"))
            {
                if (!section.Expanded)
                    return;

                Scene scene = SceneManager.GetActiveScene();
                GodMenuGUI.Field("Open Scene", scene.name);

                bool playing = EditorApplication.isPlaying;
                bool[] enabled = { playing && !string.IsNullOrEmpty(scene.path), !playing && scene.isDirty };

                switch (GodMenuGUI.ButtonRow(SceneLabels, enabled))
                {
                    case 0:
                        Time.timeScale = 1f;
                        EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
                        break;
                    case 1:
                        EditorSceneManager.SaveScene(scene);
                        break;
                }
            }
        }
    }
}
