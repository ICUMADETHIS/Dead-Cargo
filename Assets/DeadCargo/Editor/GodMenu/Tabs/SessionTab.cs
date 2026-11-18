using DeadCargo.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Play mode, time and scene controls, and the last scene change's load report.</summary>
    internal sealed class SessionTab : GodMenuTab
    {
        private const string RestartTitle = "RESTARTING";
        private const int ShownWork = 6;

        private static readonly float[] TimeScalePresets = { 0.25f, 0.5f, 1f, 2f, 4f };
        private static readonly string[] TimeScaleLabels = { "0.25x", "0.5x", "1x", "2x", "4x" };
        private static readonly string[] SceneLabels = { "Restart Scene", "Save Scene" };
        private static readonly string[] ReportLabels = { "Copy Report" };

        public override string Title => "Session";
        public override GodMenuGroup Group => GodMenuGroup.Game;
        public override int Order => 0;

        public override string Summary
        {
            get
            {
                if (!EditorApplication.isPlaying)
                    return null;
                if (!Mathf.Approximately(Time.timeScale, 1f))
                    return $"{Time.timeScale:0.##}x speed";
                if (LoadReport.IsRecording)
                    return "Timing load";

                LoadRecord last = LoadReport.Last;
                return last != null ? $"Last load worst {last.WorstMilliseconds:0} ms" : null;
            }
        }

        public override void OnGUI()
        {
            DrawPlayMode();
            DrawTime();
            DrawScene();
            DrawLoadReport();
        }

        // The last scene change: its stages, its setup work by name, and the frames once the game appeared.
        private static void DrawLoadReport()
        {
            if (!EditorApplication.isPlaying)
                return;

            using (var section = new GodMenuGUI.SectionScope("Load Report"))
            {
                if (!section.Expanded)
                    return;

                LoadRecord last = LoadReport.Last;
                if (LoadReport.IsRecording)
                    GodMenuGUI.Hint($"Timing this load: the report comes {LoadReport.WatchSeconds:0} s after the game appears.");
                if (last == null)
                    return;

                GodMenuGUI.Status(last.Over50 == 0, last.IsFirstScene
                    ? $"{last.Scene} started"
                    : $"{last.Scene} shown after {last.UntilShown:0.00} s");

                if (!last.IsFirstScene)
                {
                    GodMenuGUI.Field("Load", $"{last.Load:0.00} s, switch {last.Switch:0.00} s, start-up {Seconds(last.StartUp)}");
                    GodMenuGUI.Field("Fades", $"out {last.FadeOut:0.00} s, in {last.FadeIn:0.00} s, waiting {last.Waiting:0.00} s");
                    DrawGate(last);
                }

                GodMenuGUI.Field("First Frames", $"{last.FirstFrames * 1000.0:0} ms");
                string collected = last.WorstCollected ? (last.WorstLabel != null ? ", garbage collection" : "garbage collection") : "";
                string blame = last.WorstLabel != null || last.WorstCollected ? $" ({last.WorstLabel}{collected})" : "";
                GodMenuGUI.Field("Worst Frame", $"{last.WorstMilliseconds:0} ms at {last.WorstAt:0.0} s{blame}");
                GodMenuGUI.Field("Slow Frames", $"{last.Over50} over 50 ms, {last.Over33} over 33 ms, {last.Collections} collections");
                GodMenuGUI.Field("Average", $"{last.AverageMilliseconds:0.0} ms over {last.Frames} frames");

                for (int i = 0; i < last.Work.Count && i < ShownWork; ++i)
                {
                    LoadWork work = last.Work[i];
                    string span = work.SpanSeconds > 0.0 ? $", ran {work.SpanSeconds:0.00} s" : "";
                    string calls = work.Calls > 1 ? $" in {work.Calls}" : "";
                    GodMenuGUI.Field(work.Label, $"{work.TotalMilliseconds:0} ms{calls}{span}");
                }

                if (GodMenuGUI.ButtonRow(ReportLabels) == 0)
                    EditorGUIUtility.systemCopyBuffer = last.Summary;
            }
        }

        // What the loading screen waited for once the scene was switched on, and how it stopped waiting.
        private static void DrawGate(LoadRecord last)
        {
            if (last.HeldBy != null)
                GodMenuGUI.Status(false, $"Gave up after {last.Waiting:0.0} s on {last.HeldBy}");
            else if (last.Settled)
                GodMenuGUI.Field("Gate", $"tasks {Seconds(last.TaskWait)}, settled after {Seconds(last.Settling)}");
            else
                GodMenuGUI.Status(false, $"Frames did not settle in {Seconds(last.Settling)}");

            foreach (LoadWait wait in last.Waits)
                GodMenuGUI.Field(wait.Label, wait.Finished ? $"waited {wait.Seconds:0.00} s" : $"unfinished after {wait.Seconds:0.00} s");
        }

        private static string Seconds(double seconds)
        {
            return seconds < 0.0 ? "-" : $"{seconds:0.00} s";
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
                        SceneFlow.LoadScene(scene.path, RestartTitle);
                        break;
                    case 1:
                        EditorSceneManager.SaveScene(scene);
                        break;
                }
            }
        }
    }
}
