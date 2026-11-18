using DeadCargo.UI;
using UnityEditor;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Player options for the on-screen HUD. They are kept between sessions and apply at once, in Play Mode or not.</summary>
    internal sealed class HudTab : GodMenuTab
    {
        public override string Title => "HUD";
        public override GodMenuGroup Group => GodMenuGroup.Game;
        public override int Order => 17;

        public override string Summary => HudSettings.ShowSteeringLine ? "steering line on" : "steering line off";

        public override void OnGUI()
        {
            using (var section = new GodMenuGUI.SectionScope("Reticle"))
            {
                if (!section.Expanded)
                    return;

                HudSettings.ShowSteeringLine = EditorGUILayout.ToggleLeft("Line out to the steering cursor", HudSettings.ShowSteeringLine);
                GodMenuGUI.Hint("The amber dot is where the mouse steers. With the line off, the dot alone shows it; with it on, a " +
                                "faint inked line runs out to the dot from the reticle while you turn.");
            }
        }
    }
}
