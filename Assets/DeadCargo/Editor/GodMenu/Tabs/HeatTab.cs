using DeadCargo.Heat;
using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Inspect and set the run's heat level.</summary>
    internal sealed class HeatTab : GodMenuTab
    {
        private static readonly string[] AdjustLabels = { "+5", "+25", "Max", "Clear" };

        private string[] tierLabels = new string[0];

        public override string Title => "Heat";
        public override GodMenuGroup Group => GodMenuGroup.Run;
        public override int Order => 20;

        public override bool IsAvailable => GodMenuContext.IsPlaying && HeatTracker.Active != null;

        public override string Summary
        {
            get
            {
                HeatTracker tracker = GodMenuContext.IsPlaying ? HeatTracker.Active : null;
                if (tracker == null)
                    return null;

                string level = $"{tracker.CurrentTier.Label}  {tracker.Heat:0}";
                return tracker.IsDecayHeld ? $"{level}  ·  held" : level;
            }
        }

        public override void OnGUI()
        {
            HeatTracker tracker = GodMenuContext.IsPlaying ? HeatTracker.Active : null;
            using (new EditorGUI.DisabledScope(tracker == null))
            {
                DrawLevel(tracker);
                DrawTiers(tracker);
            }
        }

        private static void DrawLevel(HeatTracker tracker)
        {
            using (var section = new GodMenuGUI.SectionScope("Level"))
            {
                if (!section.Expanded)
                    return;

                if (tracker == null)
                {
                    GodMenuGUI.Bar(0f, "0 / 0", GodMenuGUI.Neutral);
                    return;
                }

                HeatTier tier = tracker.CurrentTier;
                GodMenuGUI.Bar(tracker.NormalizedHeat, $"{tracker.Heat:0} / {tracker.MaxHeat:0}   {tier.Label}", tier.Color);

                float heat = EditorGUILayout.Slider("Heat", tracker.Heat, 0f, tracker.MaxHeat);
                if (!Mathf.Approximately(heat, tracker.Heat))
                    tracker.Set(heat);

                bool hold = EditorGUILayout.Toggle(new GUIContent("Hold Heat", "Heat does not decay, so the tier and its hunters stay while you test."), tracker.IsDecayHeld);
                if (hold != tracker.IsDecayHeld)
                    tracker.HoldDecay(hold);

                switch (GodMenuGUI.ButtonRow(AdjustLabels))
                {
                    case 0:
                        tracker.Add(5f);
                        break;
                    case 1:
                        tracker.Add(25f);
                        break;
                    case 2:
                        tracker.Set(tracker.MaxHeat);
                        break;
                    case 3:
                        tracker.Clear();
                        break;
                }
            }
        }

        private void DrawTiers(HeatTracker tracker)
        {
            using (var section = new GodMenuGUI.SectionScope("Jump To Tier"))
            {
                if (!section.Expanded || tracker == null)
                    return;

                if (tierLabels.Length != tracker.Tiers.Count)
                    tierLabels = new string[tracker.Tiers.Count];

                for (int i = 0; i < tierLabels.Length; ++i)
                    tierLabels[i] = tracker.Tiers[i].Label;

                // The current tier's button is tinted with its colour.
                int clicked = GodMenuGUI.ButtonRow(tierLabels, null, tracker.TierIndex, tracker.CurrentTier.Color);
                if (clicked >= 0)
                    tracker.Set(tracker.Tiers[clicked].Threshold);
            }
        }
    }
}
