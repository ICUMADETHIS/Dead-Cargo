using System;
using System.Collections.Generic;
using DeadCargo.Cargo;
using DeadCargo.Profile;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// Builds the mothership hub screen: title, credits, last run, service record, the cargo stash with selling,
    /// and the launch button.
    /// </summary>
    internal sealed class MothershipHubView
    {
        internal const int MaxStashRows = 8;

        private const float Margin = 96f;
        private const float Padding = 28f;
        private const float LeftWidth = 600f;
        private const float RunHeight = 196f;
        private const float RecordHeight = 100f;
        private const float CreditsWidth = 440f;
        private const float CreditsHeight = 118f;
        private const float StashWidth = 640f;
        private const float StashRowsTop = 90f;
        private const float StashRowHeight = 34f;
        private const float LaunchWidth = 440f;
        private const float LaunchHeight = 118f;
        private const float SellWidth = 64f;
        private const float SellAllWidth = 150f;

        // Stash columns: item, quantity, mass, value. The sell button sits right of the value.
        private static readonly float[] ColumnX = { Padding, 270f, 326f, 412f };
        private static readonly float[] ColumnWidth = { 236f, 50f, 80f, 106f };

        private static readonly Color BackgroundColor = new Color32(7, 13, 19, 255);

        private readonly HudPlate runPlate;
        private readonly Text runOutcome;
        private readonly Text runStats;
        private readonly Text runCargo;
        private readonly Text record;
        private readonly Text credits;
        private readonly HudPlate stashPlate;
        private readonly Text stashTotal;
        private readonly Text stashEmpty;
        private readonly Text[,] stashRows = new Text[MaxStashRows, 4];
        private readonly Button[] sellButtons = new Button[MaxStashRows];
        private readonly RectTransform stashFooterRule;
        private readonly Button sellAllButton;
        private readonly RectTransform sellAllKey;
        private readonly Button launchButton;
        private readonly Text launchTitle;
        private readonly Text sectorName;

        internal MothershipHubView(RectTransform parent, Action onLaunch, Action<int> onSell, Action onSellAll)
        {
            Color ink = HudFactory.Ink;
            Color muted = HudFactory.Muted;
            Vector2 topLeft = new Vector2(0f, 1f);
            Vector2 topRight = new Vector2(1f, 1f);

            HudFactory.Overlay("Background", parent, BackgroundColor);

            RectTransform header = HudFactory.Anchored("Header", parent, topLeft, 900f, 180f);
            header.anchoredPosition = new Vector2(Margin, -Margin);
            HudFactory.Label("Kicker", header, "DEAD CARGO", 0f, 0f, 400f, 24f, 20, muted);
            HudFactory.Label("Title", header, "MOTHERSHIP", 0f, 24f, 900f, 90f, 84, ink);
            HudFactory.Label("Subtitle", header, "Hangar deck  ·  Ready to launch", 0f, 116f, 900f, 30f, 24, muted);

            // Left column: last run and service record.
            runPlate = HudFactory.Plate("Last Run", parent, topLeft, LeftWidth, RunHeight);
            runPlate.rectTransform.anchoredPosition = new Vector2(Margin, -330f);
            RectTransform run = runPlate.rectTransform;
            HudFactory.Label("Kicker", run, "LAST RUN", Padding, 20f, 300f, 20f, 15, muted);
            runOutcome = HudFactory.Label("Outcome", run, "", Padding, 42f, LeftWidth - Padding * 2f, 52f, 44, ink);
            HudFactory.Rule(run, Padding, 104f, LeftWidth - Padding * 2f);
            runStats = HudFactory.Label("Stats", run, "", Padding, 116f, LeftWidth - Padding * 2f, 28f, 19, muted);
            HudFactory.Fit(runStats, 13);
            runCargo = HudFactory.Label("Cargo", run, "", Padding, 146f, LeftWidth - Padding * 2f, 30f, 22, ink);
            HudFactory.Fit(runCargo, 14);

            HudPlate recordPlate = HudFactory.Plate("Service Record", parent, topLeft, LeftWidth, RecordHeight);
            recordPlate.rectTransform.anchoredPosition = new Vector2(Margin, -550f);
            HudFactory.Label("Kicker", recordPlate.rectTransform, "SERVICE RECORD", Padding, 20f, 300f, 20f, 15, muted);
            record = HudFactory.Label("Record", recordPlate.rectTransform, "", Padding, 46f, LeftWidth - Padding * 2f, 32f, 24, ink);

            // Right column: credits and stash.
            HudPlate creditsPlate = HudFactory.Plate("Credits", parent, topRight, CreditsWidth, CreditsHeight);
            creditsPlate.rectTransform.anchoredPosition = new Vector2(-Margin, -Margin);
            creditsPlate.SetAccent(HudFactory.CargoColor);
            HudFactory.Label("Kicker", creditsPlate.rectTransform, "CREDITS", Padding, 18f, 300f, 20f, 15, muted);
            credits = HudFactory.Label("Credits", creditsPlate.rectTransform, "", Padding, 42f, CreditsWidth - Padding * 2f, 56f, 48, ink, numeric: true);
            HudFactory.Fit(credits, 24);

            stashPlate = HudFactory.Plate("Stash", parent, topRight, StashWidth, 200f);
            stashPlate.rectTransform.anchoredPosition = new Vector2(-Margin, -238f);
            stashPlate.SetAccent(HudFactory.CargoColor);
            RectTransform stash = stashPlate.rectTransform;
            HudFactory.Label("Title", stash, "CARGO STASH", Padding, 16f, 300f, 28f, 22, ink);
            stashTotal = HudFactory.Label("Total", stash, "", 300f, 16f, StashWidth - Padding - 300f, 28f, 18, HudFactory.CargoColor, TextAnchor.MiddleRight, true);
            HudFactory.Rule(stash, Padding, 52f, StashWidth - Padding * 2f);

            string[] headers = { "ITEM", "QTY", "MASS", "VALUE" };
            for (int column = 0; column < headers.Length; column++)
            {
                TextAnchor alignment = column == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                HudFactory.Label(headers[column] + " column", stash, headers[column], ColumnX[column], 62f, ColumnWidth[column], 18f, 13, muted, alignment);
            }

            float sellX = StashWidth - Padding - SellWidth;
            for (int i = 0; i < MaxStashRows; i++)
            {
                float y = StashRowsTop + i * StashRowHeight;
                stashRows[i, 0] = HudFactory.Label("Item", stash, "", ColumnX[0], y, ColumnWidth[0], StashRowHeight, 19, ink);
                HudFactory.Fit(stashRows[i, 0], 13);
                for (int column = 1; column < 4; column++)
                {
                    stashRows[i, column] = HudFactory.Label(headers[column], stash, "", ColumnX[column], y, ColumnWidth[column], StashRowHeight, 14, ink,
                        TextAnchor.MiddleRight, true);
                    HudFactory.Fit(stashRows[i, column], 10);
                }

                int row = i;
                sellButtons[i] = HudFactory.TextButton("Sell", stash, "SELL", sellX, y + 5f, SellWidth, StashRowHeight - 10f, 14,
                    HudFactory.CargoColor, () => onSell(row));
            }

            stashEmpty = HudFactory.Label("Empty", stash, "Stash empty. Extract cargo to fill it.", Padding, StashRowsTop, StashWidth - Padding * 2f,
                StashRowHeight, 18, muted);
            stashFooterRule = HudFactory.Rule(stash, Padding, 0f, StashWidth - Padding * 2f).rectTransform;
            sellAllButton = HudFactory.TextButton("Sell All", stash, "SELL ALL", StashWidth - Padding - SellAllWidth, 0f, SellAllWidth, 34f, 16,
                HudFactory.CargoColor, onSellAll);
            sellAllKey = HudFactory.Key(stash, "S", 0f, 0f);

            // Bottom right: launch.
            HudPlate launchPlate = HudFactory.Plate("Launch", parent, new Vector2(1f, 0f), LaunchWidth, LaunchHeight);
            launchPlate.rectTransform.anchoredPosition = new Vector2(-Margin, Margin);
            launchPlate.SetAccent(HudFactory.JumpColor);
            RectTransform launch = launchPlate.rectTransform;
            sectorName = HudFactory.Label("Sector", launch, "", Padding, 18f, LaunchWidth - Padding * 2f, 22f, 16, muted);
            launchTitle = HudFactory.Label("Launch title", launch, "LAUNCH RUN", Padding, 42f, 300f, 54f, 46, ink);
            HudFactory.Key(launch, "ENTER", LaunchWidth - Padding - 54f, 62f, 54f);
            launchButton = HudFactory.MakeButton(launchPlate, onLaunch);
        }

        internal void SetCredits(int amount)
        {
            credits.text = $"{amount:N0} cr";
        }

        internal void SetRecord(int extracted, int lost)
        {
            record.text = $"Extracted {extracted}   ·   Lost {lost}   ·   Runs {extracted + lost}";
        }

        internal void ShowLastRun(bool hasRun, RunSummary run)
        {
            if (!hasRun)
            {
                runPlate.SetAccent(HudFactory.Muted);
                runOutcome.text = "NO RUNS YET";
                runOutcome.color = HudFactory.Muted;
                runStats.text = "Launch into a sector, take what you can, and jump home.";
                runCargo.text = "";
                return;
            }

            Color accent = run.Extracted ? HudFactory.SuccessColor : HudFactory.WarningColor;
            runPlate.SetAccent(accent);
            runOutcome.text = run.Extracted ? "EXTRACTED" : run.ShipLost ? "SHIP LOST" : "RUN LOST";
            runOutcome.color = accent;

            string peak = string.IsNullOrEmpty(run.PeakHeat) ? "n/a" : run.PeakHeat;
            runStats.text = $"Run time {FormatDuration(run.Duration)}   ·   Peak heat {peak}   ·   Authority downed {run.AuthorityShipsDestroyed}";
            string cargo = $"{(run.Extracted ? "Cargo banked" : "Cargo lost")}   {run.CargoValue:N0} cr   ·   {run.CargoMass:0.#} t";
            string ship = !run.ShipLost ? "" : run.ShipLossFee > 0 ? $"   ·   Ship −{run.ShipLossFee:N0} cr" : "   ·   Basic hull issued";
            runCargo.text = cargo + ship;
            runCargo.color = run.Extracted ? HudFactory.Ink : HudFactory.WarningColor;
        }

        /// <summary>Lists the stash. Rows past the limit are summed into a last row, which has no sell button.</summary>
        internal void SetStash(IReadOnlyList<CargoStack> stacks)
        {
            int count = stacks.Count;
            bool overflow = count > MaxStashRows;
            int listed = overflow ? MaxStashRows - 1 : count;
            int totalValue = 0;
            foreach (CargoStack stack in stacks)
                totalValue += stack.Value;

            for (int i = 0; i < MaxStashRows; i++)
            {
                bool visible = i < listed || (overflow && i == MaxStashRows - 1);
                for (int column = 0; column < 4; column++)
                    stashRows[i, column].enabled = visible;
                sellButtons[i].gameObject.SetActive(i < listed);

                if (i < listed)
                {
                    CargoStack stack = stacks[i];
                    SetRow(i, stack.Item.DisplayName, stack.Quantity.ToString(), stack.Mass, stack.Value);
                }
                else if (visible)
                {
                    float mass = 0f;
                    int value = 0;
                    for (int j = listed; j < count; j++)
                    {
                        mass += stacks[j].Mass;
                        value += stacks[j].Value;
                    }

                    SetRow(i, $"+{count - listed} more", "", mass, value);
                }
            }

            stashEmpty.enabled = count == 0;
            stashTotal.text = count > 0 ? $"{totalValue:N0} cr" : "";

            float footer = StashRowsTop + Mathf.Max(1, overflow ? MaxStashRows : count) * StashRowHeight + 10f;
            HudFactory.SetOffset(stashFooterRule, Padding, footer);
            HudFactory.SetOffset(sellAllButton.transform as RectTransform, StashWidth - Padding - SellAllWidth, footer + 14f);
            HudFactory.SetOffset(sellAllKey, StashWidth - Padding - SellAllWidth - 26f, footer + 22f);
            sellAllButton.interactable = count > 0;
            stashPlate.rectTransform.sizeDelta = new Vector2(StashWidth, footer + 64f);
        }

        internal void SetLaunch(string sector, bool available)
        {
            sectorName.text = available ? $"SECTOR  ·  {sector.ToUpperInvariant()}" : "NO SECTOR SET UP";
            launchTitle.color = available ? HudFactory.Ink : HudFactory.Muted;
            launchButton.interactable = available;
        }

        private void SetRow(int row, string item, string quantity, float mass, int value)
        {
            stashRows[row, 0].text = item;
            stashRows[row, 1].text = quantity;
            stashRows[row, 2].text = $"{mass:0.#} t";
            stashRows[row, 3].text = $"{value:N0} cr";
        }

        private static string FormatDuration(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
