using DeadCargo.Debugging;
using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>
    /// The in-game debug overlay: show or hide it, place it, and switch its channels. Settings are kept between
    /// sessions, so channels can be chosen before pressing Play. In game, F3 and F4 change the same settings.
    /// </summary>
    internal sealed class DebugTab : GodMenuTab
    {
        private static readonly string[] CornerNames = { "Top Left", "Top Right", "Bottom Left", "Bottom Right" };
        private static readonly string[] ChannelLabels = { "All", "None", "Defaults" };
        private static readonly string[] LogLabels = { "Clear Log" };
        private static readonly string[] FrameCapLabels = { "Off", "30", "60", "120", "144" };
        private static readonly int[] FrameCapPresets = { 0, 30, 60, 120, 144 };
        private const int DefaultFrameCap = 60;

        private readonly GUIContent[] channelContent = new GUIContent[DebugChannels.Count];
        private float fps;
        private bool running;

        public DebugTab()
        {
            for (int i = 0; i < DebugChannels.Count; ++i)
            {
                var channel = (DebugChannel)i;
                channelContent[i] = new GUIContent($"{DebugChannels.Name(channel)}  [{DebugChannels.PickerKeyLabel(channel)}]",
                    DebugChannels.Description(channel));
            }
        }

        public override string Title => "Debug Overlay";
        public override GodMenuGroup Group => GodMenuGroup.Game;
        public override int Order => 20;

        public override string Summary
        {
            get
            {
                if (!DebugSettings.OverlayVisible)
                    return "hidden";

                int channels = DebugSettings.EnabledCount;
                string channelText = channels == 1 ? "1 channel" : $"{channels} channels";
                if (DebugSettings.FrameCap > 0)
                    channelText += $" · cap {DebugSettings.FrameCap}";

                return running ? $"{fps:0} fps · {channelText}" : channelText;
            }
        }

        public override void OnUpdate()
        {
            DebugOverlay overlay = GodMenuContext.IsPlaying ? DebugOverlay.Instance : null;
            running = overlay != null;
            fps = running ? overlay.Stats.Fps : 0f;
        }

        public override void OnGUI()
        {
            DrawOverlay();
            DrawFrameRate();
            DrawChannels();
            DrawLog();
        }

        private static void DrawOverlay()
        {
            using (var section = new GodMenuGUI.SectionScope("Overlay"))
            {
                if (!section.Expanded)
                    return;

                DebugSettings.OverlayVisible = EditorGUILayout.ToggleLeft($"Show overlay  [{DebugOverlay.OverlayKey}]", DebugSettings.OverlayVisible);
                DebugSettings.Corner = (DebugCorner)EditorGUILayout.Popup("Corner", (int)DebugSettings.Corner, CornerNames);
                DebugSettings.TextScale = EditorGUILayout.Slider("Size", DebugSettings.TextScale, DebugSettings.MinTextScale, DebugSettings.MaxTextScale);
                GodMenuGUI.Hint($"In game: {DebugOverlay.OverlayKey} shows or hides it, {DebugOverlay.PickerKey} opens the channel picker.");
            }
        }

        private static void DrawFrameRate()
        {
            using (var section = new GodMenuGUI.SectionScope("Frame Rate"))
            {
                if (!section.Expanded)
                    return;

                int cap = DebugSettings.FrameCap;
                bool capped = EditorGUILayout.ToggleLeft("Cap frame rate", cap > 0);
                if (capped != cap > 0)
                    cap = capped ? DefaultFrameCap : 0;

                if (capped)
                    cap = EditorGUILayout.IntSlider("Cap (fps)", cap, DebugSettings.MinFrameCap, DebugSettings.MaxFrameCap);

                int preset = GodMenuGUI.ButtonRow(FrameCapLabels);
                if (preset >= 0)
                    cap = FrameCapPresets[preset];

                DebugSettings.FrameCap = cap;
                GodMenuGUI.Hint("Applies in Play Mode, and is kept between sessions. A cap switches vsync off until Play stops.");
                if (GodMenuContext.IsPlaying)
                {
                    int target = Application.targetFrameRate;
                    GodMenuGUI.Hint($"Now: vsync {QualitySettings.vSyncCount}, target {(target > 0 ? target.ToString() : "none")}");
                }
            }
        }

        private void DrawChannels()
        {
            using (var section = new GodMenuGUI.SectionScope("Channels"))
            {
                if (!section.Expanded)
                    return;

                for (int i = 0; i < DebugChannels.Count; ++i)
                {
                    var channel = (DebugChannel)i;
                    DebugSettings.Set(channel, EditorGUILayout.ToggleLeft(channelContent[i], DebugSettings.IsOn(channel)));
                }

                switch (GodMenuGUI.ButtonRow(ChannelLabels))
                {
                    case 0:
                        DebugSettings.SetAll(true);
                        break;
                    case 1:
                        DebugSettings.SetAll(false);
                        break;
                    case 2:
                        DebugSettings.ResetToDefaults();
                        break;
                }
            }
        }

        private void DrawLog()
        {
            if (!running)
                return;

            using (var section = new GodMenuGUI.SectionScope("Log"))
            {
                if (!section.Expanded)
                    return;

                int errors = DebugLog.Errors;
                int warnings = DebugLog.Warnings;
                GodMenuGUI.Status(errors == 0, $"{errors} errors, {warnings} warnings this session");
                if (GodMenuGUI.ButtonRow(LogLabels) == 0)
                    DebugLog.Clear();
            }
        }
    }
}
