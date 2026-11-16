using DeadCargo.Cargo;
using DeadCargo.Extraction;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>Builds the end-of-run screen: a dimmed backdrop, a centred report plate and a full-screen jump flash.</summary>
    internal sealed class RunResultView
    {
        private const float Width = 600f;
        private const float Padding = 28f;
        private const float ContentWidth = Width - Padding * 2f;
        private const float StatColumnWidth = ContentWidth / 3f;
        private const int MaxRows = 6;
        private const float RowsTop = 234f;
        private const float RowHeight = 26f;
        private const float FooterKeyWidth = 54f;
        private const float FooterGap = 8f;

        // Manifest columns: left edge and width.
        private static readonly float[] ColumnX = { Padding, 296f, 352f, 446f };
        private static readonly float[] ColumnWidth = { 262f, 50f, 88f, 126f };

        private static readonly Color BackdropColor = new Color(0.02f, 0.04f, 0.06f, 0.82f);

        private readonly GameObject content;
        private readonly CanvasGroup contentGroup;
        private readonly Image flash;
        private readonly HudPlate panel;
        private readonly Text outcome;
        private readonly Text summary;
        private readonly Text runTime;
        private readonly Text peakHeat;
        private readonly Text shipsDestroyed;
        private readonly Text emptyHold;
        private readonly Text[,] rows = new Text[MaxRows, 4];
        private readonly RectTransform totalRule;
        private readonly Text totalLabel;
        private readonly Text totalValue;
        private readonly Text feeLabel;
        private readonly Text feeValue;
        private readonly RectTransform footerKey;
        private readonly Text footerLabel;

        internal RunResultView(RectTransform parent)
        {
            Color ink = HudFactory.Ink;
            Color muted = HudFactory.Muted;

            RectTransform contentRect = HudFactory.Stretch("Content", parent);
            content = contentRect.gameObject;
            contentGroup = content.AddComponent<CanvasGroup>();
            contentGroup.interactable = false;

            // The report is modal: the backdrop catches clicks meant for kit buttons underneath, such as pause.
            HudFactory.Overlay("Backdrop", contentRect, BackdropColor).raycastTarget = true;

            panel = HudFactory.Plate("Run Report", contentRect, new Vector2(0.5f, 0.5f), Width, 400f);
            RectTransform p = panel.rectTransform;

            HudFactory.Label("Kicker", p, "RUN REPORT", Padding, 20, 300, 20, 15, muted);
            outcome = HudFactory.Label("Outcome", p, "", Padding, 38, ContentWidth, 56, 48, ink);
            summary = HudFactory.Label("Summary", p, "", Padding, 94, ContentWidth, 24, 18, muted);
            HudFactory.Rule(p, Padding, 130, ContentWidth);

            StatTitle(p, "RUN TIME", 0);
            StatTitle(p, "PEAK HEAT", 1);
            StatTitle(p, "AUTHORITY DOWNED", 2);
            runTime = StatValue(p, "Run time", 0, true);
            peakHeat = StatValue(p, "Peak heat", 1, false);
            shipsDestroyed = StatValue(p, "Authority downed", 2, true);
            HudFactory.Rule(p, Padding, 198, ContentWidth);

            string[] headers = { "ITEM", "QTY", "MASS", "VALUE" };
            for (int column = 0; column < headers.Length; column++)
            {
                TextAnchor alignment = column == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                HudFactory.Label(headers[column] + " column", p, headers[column], ColumnX[column], 208, ColumnWidth[column], 18, 13, muted, alignment);
            }

            for (int i = 0; i < MaxRows; i++)
            {
                float y = RowsTop + i * RowHeight;
                rows[i, 0] = HudFactory.Label("Item", p, "", ColumnX[0], y, ColumnWidth[0], RowHeight, 19, ink);
                HudFactory.Fit(rows[i, 0], 13);
                for (int column = 1; column < 4; column++)
                {
                    rows[i, column] = HudFactory.Label(headers[column], p, "", ColumnX[column], y, ColumnWidth[column], RowHeight, 14, ink, TextAnchor.MiddleRight, true);
                    HudFactory.Fit(rows[i, column], 10);
                }
            }

            emptyHold = HudFactory.Label("Empty hold", p, "Hold empty", Padding, RowsTop, ContentWidth, RowHeight, 18, muted);
            totalRule = HudFactory.Rule(p, Padding, 0, ContentWidth).rectTransform;
            totalLabel = HudFactory.Label("Total label", p, "", Padding, 0, 300, 26, 18, ink);
            totalValue = HudFactory.Label("Total value", p, "", 300, 0, Width - Padding - 300, 40, 32, ink, TextAnchor.MiddleRight, true);
            HudFactory.Fit(totalValue, 18);
            feeLabel = HudFactory.Label("Fee label", p, "", Padding, 0, 300, 24, 16, muted);
            feeValue = HudFactory.Label("Fee value", p, "", 300, 0, Width - Padding - 300, 24, 18, ink, TextAnchor.MiddleRight, true);
            footerKey = HudFactory.Key(p, "ENTER", 0, 0, FooterKeyWidth);
            footerLabel = HudFactory.Label("Footer action", p, "", 0, 0, ContentWidth, 22, 17, ink);

            flash = HudFactory.Overlay("Jump Flash", parent, new Color(1f, 1f, 1f, 0f));
            flash.enabled = false;
            content.SetActive(false);
        }

        /// <summary>Fills the report, labels the Enter action and makes it visible. Fade it in with <see cref="SetOpacity"/>.</summary>
        internal void Show(RunResult result, string action)
        {
            // Active first, so the footer label can measure itself.
            content.SetActive(true);
            bool extracted = result.Outcome == RunOutcome.Extracted;
            Color accent = extracted ? HudFactory.SuccessColor : HudFactory.WarningColor;

            panel.SetAccent(accent);
            outcome.text = OutcomeTitle(result);
            outcome.color = accent;
            summary.text = extracted
                ? result.Route == ExtractionRoute.ExtractionPoint ? "Extraction point reached. The hold made it out." : "Jump complete. The hold made it out."
                : result.ShipLost ? "The ship is gone, and its hold went with it."
                : "You did not make it back. The hold is lost.";

            runTime.text = FormatDuration(result.Duration);
            if (result.PeakHeat.HasValue)
            {
                peakHeat.text = result.PeakHeat.Value.Label;
                peakHeat.color = result.PeakHeat.Value.Color;
            }
            else
            {
                peakHeat.text = "n/a";
                peakHeat.color = HudFactory.Muted;
            }
            shipsDestroyed.text = result.AuthorityShipsDestroyed.ToString();

            int visibleRows = FillRows(result, extracted ? HudFactory.Ink : HudFactory.Muted);

            float y = RowsTop + Mathf.Max(1, visibleRows) * RowHeight + 12f;
            HudFactory.SetOffset(totalRule, Padding, y);
            totalLabel.text = $"{(extracted ? "CARGO BANKED" : "CARGO LOST")}  ·  {result.CargoMass:0.#} t";
            totalLabel.color = accent;
            HudFactory.SetOffset(totalLabel.rectTransform, Padding, y + 18f);
            totalValue.text = $"{result.CargoValue:N0} cr";
            totalValue.color = extracted ? HudFactory.Ink : HudFactory.WarningColor;
            HudFactory.SetOffset(totalValue.rectTransform, 300f, y + 11f);

            // A lost ship adds a fee line: the charge, or a free basic hull when the player could not pay.
            float footer = y + 66f;
            feeLabel.enabled = feeValue.enabled = result.ShipLost;
            if (result.ShipLost)
            {
                bool paid = result.ShipLossFee > 0;
                feeLabel.text = paid ? "SHIP REPLACEMENT" : "BASIC HULL ISSUED";
                feeValue.text = paid ? $"−{result.ShipLossFee:N0} cr" : "no charge";
                feeValue.color = paid ? HudFactory.WarningColor : HudFactory.Muted;
                HudFactory.SetOffset(feeLabel.rectTransform, Padding, y + 54f);
                HudFactory.SetOffset(feeValue.rectTransform, 300f, y + 54f);
                footer += 32f;
            }

            // The key cap sits just left of the action label, whatever its length.
            footerLabel.text = action;
            float labelWidth = Mathf.Ceil(footerLabel.preferredWidth);
            float labelX = Width - Padding - labelWidth;
            footerLabel.rectTransform.sizeDelta = new Vector2(labelWidth, footerLabel.rectTransform.sizeDelta.y);
            HudFactory.SetOffset(footerLabel.rectTransform, labelX, footer);
            HudFactory.SetOffset(footerKey, labelX - FooterGap - FooterKeyWidth, footer + 2f);
            panel.rectTransform.sizeDelta = new Vector2(Width, footer + 44f);
        }

        internal void SetOpacity(float alpha)
        {
            contentGroup.alpha = alpha;
        }

        internal void SetFlash(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            if (Mathf.Approximately(flash.color.a, alpha))
                return;

            flash.color = new Color(1f, 1f, 1f, alpha);
            flash.enabled = alpha > 0f;
        }

        // Lists the cargo. When it does not fit, the last row sums up the rest. Returns the rows used.
        private int FillRows(RunResult result, Color tint)
        {
            int stackCount = result.Cargo.Count;
            bool overflow = stackCount > MaxRows;
            int listed = overflow ? MaxRows - 1 : stackCount;

            for (int i = 0; i < MaxRows; i++)
            {
                bool visible = i < listed || (overflow && i == MaxRows - 1);
                for (int column = 0; column < 4; column++)
                {
                    rows[i, column].enabled = visible;
                    rows[i, column].color = tint;
                }

                if (!visible)
                    continue;

                if (i < listed)
                {
                    CargoStack stack = result.Cargo[i];
                    SetRow(i, stack.Item.DisplayName, stack.Quantity.ToString(), stack.Mass, stack.Value);
                    continue;
                }

                float mass = 0f;
                int value = 0;
                for (int j = listed; j < stackCount; j++)
                {
                    mass += result.Cargo[j].Mass;
                    value += result.Cargo[j].Value;
                }

                SetRow(i, $"+{stackCount - listed} more", "", mass, value);
            }

            emptyHold.enabled = stackCount == 0;
            return overflow ? MaxRows : stackCount;
        }

        private void SetRow(int row, string item, string quantity, float mass, int value)
        {
            rows[row, 0].text = item;
            rows[row, 1].text = quantity;
            rows[row, 2].text = $"{mass:0.#} t";
            rows[row, 3].text = $"{value:N0} cr";
        }

        private static void StatTitle(RectTransform parent, string title, int column)
        {
            HudFactory.Label(title, parent, title, Padding + column * StatColumnWidth, 142, StatColumnWidth - 8f, 18, 14, HudFactory.Muted);
        }

        private static Text StatValue(RectTransform parent, string name, int column, bool numeric)
        {
            Text value = HudFactory.Label(name, parent, "", Padding + column * StatColumnWidth, 160, StatColumnWidth - 8f, 30, 26, HudFactory.Ink,
                TextAnchor.MiddleLeft, numeric);
            HudFactory.Fit(value, 16);
            return value;
        }

        private static string OutcomeTitle(RunResult result)
        {
            if (result.Outcome == RunOutcome.Extracted)
                return "EXTRACTED";

            return result.ShipLost ? "SHIP LOST" : "RUN LOST";
        }

        private static string FormatDuration(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
