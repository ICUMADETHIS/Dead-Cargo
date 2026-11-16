using System;
using DeadCargo.Extraction;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>Builds the mothership hub screen: a title block, the last run's report and the launch button.</summary>
    internal sealed class MothershipHubView
    {
        private const float Margin = 96f;
        private const float Padding = 28f;
        private const float RunWidth = 600f;
        private const float RunHeight = 196f;
        private const float LaunchWidth = 440f;
        private const float LaunchHeight = 118f;

        private static readonly Color BackgroundColor = new Color32(7, 13, 19, 255);

        private readonly Text runOutcome;
        private readonly Text runStats;
        private readonly Text runCargo;
        private readonly HudPlate runPlate;
        private readonly HudPlate launchPlate;
        private readonly Button launchButton;
        private readonly Text launchTitle;
        private readonly Text sectorName;

        internal MothershipHubView(RectTransform parent, Action onLaunch)
        {
            Color ink = HudFactory.Ink;
            Color muted = HudFactory.Muted;

            HudFactory.Overlay("Background", parent, BackgroundColor);

            RectTransform header = HudFactory.Anchored("Header", parent, new Vector2(0f, 1f), 900f, 180f);
            header.anchoredPosition = new Vector2(Margin, -Margin);
            HudFactory.Label("Kicker", header, "DEAD CARGO", 0f, 0f, 400f, 24f, 20, muted);
            HudFactory.Label("Title", header, "MOTHERSHIP", 0f, 24f, 900f, 90f, 84, ink);
            HudFactory.Label("Subtitle", header, "Hangar deck  ·  Ready to launch", 0f, 116f, 900f, 30f, 24, muted);

            runPlate = HudFactory.Plate("Last Run", parent, new Vector2(0f, 1f), RunWidth, RunHeight);
            runPlate.rectTransform.anchoredPosition = new Vector2(Margin, -330f);
            RectTransform run = runPlate.rectTransform;
            HudFactory.Label("Kicker", run, "LAST RUN", Padding, 20f, 300f, 20f, 15, muted);
            runOutcome = HudFactory.Label("Outcome", run, "", Padding, 42f, RunWidth - Padding * 2f, 52f, 44, ink);
            HudFactory.Rule(run, Padding, 104f, RunWidth - Padding * 2f);
            runStats = HudFactory.Label("Stats", run, "", Padding, 116f, RunWidth - Padding * 2f, 28f, 19, muted);
            HudFactory.Fit(runStats, 13);
            runCargo = HudFactory.Label("Cargo", run, "", Padding, 146f, RunWidth - Padding * 2f, 30f, 22, ink);
            HudFactory.Fit(runCargo, 14);

            launchPlate = HudFactory.Plate("Launch", parent, new Vector2(1f, 0f), LaunchWidth, LaunchHeight);
            launchPlate.rectTransform.anchoredPosition = new Vector2(-Margin, Margin);
            launchPlate.SetAccent(HudFactory.JumpColor);
            RectTransform launch = launchPlate.rectTransform;
            sectorName = HudFactory.Label("Sector", launch, "", Padding, 18f, LaunchWidth - Padding * 2f, 22f, 16, muted);
            launchTitle = HudFactory.Label("Launch title", launch, "LAUNCH RUN", Padding, 42f, 300f, 54f, 46, ink);
            HudFactory.Key(launch, "ENTER", LaunchWidth - Padding - 54f, 62f, 54f);

            // The plate doubles as the button graphic, so hover and press tint the whole plate.
            launchPlate.raycastTarget = true;
            launchButton = launchPlate.gameObject.AddComponent<Button>();
            launchButton.targetGraphic = launchPlate;
            launchButton.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(0.82f, 0.95f, 1f),
                pressedColor = new Color(0.6f, 0.72f, 0.78f),
                selectedColor = Color.white,
                disabledColor = new Color(0.55f, 0.55f, 0.55f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            launchButton.onClick.AddListener(() => onLaunch());
        }

        internal void ShowLastRun(RunResult result)
        {
            if (result == null)
            {
                runPlate.SetAccent(HudFactory.Muted);
                runOutcome.text = "NO RUNS YET";
                runOutcome.color = HudFactory.Muted;
                runStats.text = "Launch into a sector, take what you can, and jump home.";
                runCargo.text = "";
                return;
            }

            bool extracted = result.Outcome == RunOutcome.Extracted;
            Color accent = extracted ? HudFactory.SuccessColor : HudFactory.WarningColor;
            runPlate.SetAccent(accent);
            runOutcome.text = extracted ? "EXTRACTED" : "SHIP LOST";
            runOutcome.color = accent;

            string peak = result.PeakHeat.HasValue ? result.PeakHeat.Value.Label : "n/a";
            runStats.text = $"Run time {FormatDuration(result.Duration)}   ·   Peak heat {peak}   ·   Authority downed {result.AuthorityShipsDestroyed}";
            runCargo.text = $"{(extracted ? "Cargo banked" : "Cargo lost")}   {result.CargoValue:N0} cr   ·   {result.CargoMass:0.#} t";
            runCargo.color = extracted ? HudFactory.Ink : HudFactory.WarningColor;
        }

        internal void SetLaunch(string sector, bool available)
        {
            sectorName.text = available ? $"SECTOR  ·  {sector.ToUpperInvariant()}" : "NO SECTOR SET UP";
            launchTitle.color = available ? HudFactory.Ink : HudFactory.Muted;
            launchButton.interactable = available;
        }

        private static string FormatDuration(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
