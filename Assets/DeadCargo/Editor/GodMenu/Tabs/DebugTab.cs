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
