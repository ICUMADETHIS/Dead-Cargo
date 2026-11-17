using UnityEditor;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Plays the game in a floating window of its own, at a 16:9 size, instead of the editor's Game tab.</summary>
    internal sealed class PlayWindowTab : GodMenuTab
    {
        private static readonly string[] SizeLabels = BuildSizeLabels();
        private static readonly string[] OpenLabels = { "Pop Out Now" };
        private static readonly string[] PlaceLabels = { "Centre Next Time" };

        public override string Title => "Play Window";
        public override GodMenuGroup Group => GodMenuGroup.Game;
        public override int Order => 15;

        public override string Summary
        {
            get
            {
                if (!PlayWindow.Enabled)
                    return "in the Game tab";

                var size = PlayWindow.Sizes[PlayWindow.SizeIndex];
                return $"pop-out · {size.x}×{size.y}";
            }
        }

        public override void OnGUI()
        {
            using (var section = new GodMenuGUI.SectionScope("Window"))
            {
                if (!section.Expanded)
                    return;

                PlayWindow.Enabled = EditorGUILayout.ToggleLeft("Pop the game out into its own window on Play", PlayWindow.Enabled);
                PlayWindow.SizeIndex = EditorGUILayout.Popup("Size", PlayWindow.SizeIndex, SizeLabels);

                if (PlayWindow.IsOpen)
                {
                    var picture = PlayWindow.PictureSize;
                    GodMenuGUI.Field("Picture", $"{picture.x} × {picture.y} px");
                    GodMenuGUI.Status(PlayWindow.BlankedCount > 0, PlayWindow.BlankedCount > 0
                        ? $"{PlayWindow.BlankedCount} other Game view{(PlayWindow.BlankedCount == 1 ? "" : "s")} on Display 8: drawn once"
                        : "No other Game view to blank");
                }

                if (GodMenuContext.IsPlaying && !PlayWindow.IsOpen && GodMenuGUI.ButtonRow(OpenLabels) == 0)
                    PlayWindow.Open();

                if (GodMenuGUI.ButtonRow(PlaceLabels) == 0)
                    PlayWindow.ForgetPlacement();

                GodMenuGUI.Hint("The window opens where you last left it and closes when Play stops; the size is the game picture, " +
                                "16:9 like the HUD is laid out for. While it is open the docked Game tab is switched to Display 8 " +
                                "and draws nothing, so the game is only drawn once; it gets Display 1 back when Play stops.");
            }
        }

        private static string[] BuildSizeLabels()
        {
            var labels = new string[PlayWindow.Sizes.Length];
            for (int i = 0; i < labels.Length; ++i)
                labels[i] = $"{PlayWindow.Sizes[i].x} × {PlayWindow.Sizes[i].y}";
            return labels;
        }
    }
}
