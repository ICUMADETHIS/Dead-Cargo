using DeadCargo.Cargo;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>Builds the run HUD plates and stacks the visible ones up from the bottom-right corner.</summary>
    internal sealed class ExtractionHudView
    {
        internal const int RowsPerPage = 5;

        private const float Width = 356f;
        private const float HeatHeight = 76f;
        private const float JumpHeight = 76f;
        private const float PointHeight = 76f;
        private const float CargoHeight = 106f;
        private const float NoticeHeight = 37f;
        private const float Gutter = 8f;
        private const float RightMargin = 37f;
        private const float BottomMargin = 34f;
        private const float ManifestRowsTop = 65f;
        private const float RowHeight = 24f;

        private static readonly Vector2 BottomRight = new Vector2(1f, 0f);

        private readonly HudPlate heatPlate;
        private readonly HudPlate jumpPlate;
        private readonly HudPlate pointPlate;
        private readonly HudPlate cargoPlate;
        private readonly HudPlate manifestPlate;
        private readonly HudPlate noticePlate;
        private readonly HudMeter heatMeter;
        private readonly HudMeter jumpMeter;
        private readonly HudMeter pointMeter;
        private readonly HudMeter loadMeter;
        private readonly Text heatTitle;
        private readonly Text tier;
        private readonly Text heatNumber;
        private readonly Text jumpStatus;
        private readonly Text jumpTime;
        private readonly Text jumpAction;
        private readonly Text pointStatus;
        private readonly Text pointValue;
        private readonly Text pointAction;
        private readonly RectTransform pointKey;
        private readonly Text cargoTitle;
        private readonly Text cargoMass;
        private readonly Text cargoValue;
        private readonly Text manifestToggle;
        private readonly Text manifestFooter;
        private readonly Text handling;
        private readonly Text emptyManifest;
        private readonly Text notice;
        private readonly Text[,] manifestRows = new Text[RowsPerPage, 4];
        private readonly CanvasGroup noticeGroup;

        /// <summary>
        /// Size of the bottom-right screen corner the visible plates cover, margins included, in reference pixels.
        /// Zero when no plate is shown. Updated by <see cref="Layout"/>.
        /// </summary>
        internal Vector2 StackSize { get; private set; }

        internal ExtractionHudView(RectTransform parent)
        {
            Color ink = HudFactory.Ink;
            Color muted = HudFactory.Muted;
            Color cargo = HudFactory.CargoColor;

            cargoPlate = MakePlate("Cargo Hold", parent, CargoHeight);
            cargoPlate.SetAccent(cargo);
            RectTransform cargoRect = cargoPlate.rectTransform;
            cargoTitle = HudFactory.Label("Cargo title", cargoRect, "CARGO HOLD", 19, 8, 174, 22, 17, muted);
            HudFactory.Key(cargoRect, "I", 257, 10);
            manifestToggle = HudFactory.Label("Manifest shortcut", cargoRect, "MANIFEST", 281, 8, 57, 22, 15, ink, TextAnchor.MiddleRight);
            cargoMass = HudFactory.Label("Cargo mass", cargoRect, "", 19, 30, 193, 42, 28, ink, numeric: true);
            cargoMass.supportRichText = true;
            HudFactory.Fit(cargoMass, 17);
            HudFactory.Label("Value title", cargoRect, "VALUE AT RISK", 204, 35, 133, 18, 14, cargo, TextAnchor.MiddleRight);
            cargoValue = HudFactory.Label("Cargo value", cargoRect, "", 191, 53, 146, 25, 19, ink, TextAnchor.MiddleRight, true);
            HudFactory.Fit(cargoValue, 12);
            loadMeter = HudFactory.Meter("Load meter", cargoRect, 19, 86, 318, 7);

            heatPlate = MakePlate("Authority Heat", parent, HeatHeight);
            RectTransform heatRect = heatPlate.rectTransform;
            heatTitle = HudFactory.Label("Heat title", heatRect, "AUTHORITY HEAT", 19, 7, 220, 19, 15, muted);
            heatTitle.supportRichText = true;
            HudFactory.Fit(heatTitle, 11);
            tier = HudFactory.Label("Heat tier", heatRect, "", 19, 24, 232, 32, 25, ink);
            HudFactory.Fit(tier, 17);
            heatNumber = HudFactory.Label("Heat score", heatRect, "", 242, 16, 95, 36, 27, ink, TextAnchor.MiddleRight, true);
            HudFactory.Fit(heatNumber, 17);
            heatMeter = HudFactory.Meter("Heat meter", heatRect, 19, 57, 318, 7);

            jumpPlate = MakePlate("Jump Drive", parent, JumpHeight);
            jumpPlate.SetAccent(HudFactory.JumpColor);
            RectTransform jumpRect = jumpPlate.rectTransform;
            HudFactory.Label("Jump title", jumpRect, "JUMP DRIVE", 19, 7, 203, 19, 15, muted);
            HudFactory.Key(jumpRect, "H", 257, 8);
            jumpAction = HudFactory.Label("Jump shortcut", jumpRect, "SPOOL", 281, 6, 57, 22, 15, ink, TextAnchor.MiddleRight);
            jumpStatus = HudFactory.Label("Jump status", jumpRect, "", 19, 24, 200, 32, 25, ink);
            HudFactory.Fit(jumpStatus, 17);
            jumpTime = HudFactory.Label("Jump time", jumpRect, "", 222, 24, 115, 32, 25, ink, TextAnchor.MiddleRight, true);
            HudFactory.Fit(jumpTime, 15);
            jumpMeter = HudFactory.Meter("Jump meter", jumpRect, 19, 57, 318, 7);

            pointPlate = MakePlate("Extraction Point", parent, PointHeight);
            RectTransform pointRect = pointPlate.rectTransform;
            HudFactory.Label("Point title", pointRect, "EXTRACTION POINT", 19, 7, 203, 19, 15, muted);
            pointKey = HudFactory.Key(pointRect, "U", 257, 8);
            pointAction = HudFactory.Label("Point shortcut", pointRect, "UPLINK", 281, 6, 57, 22, 15, ink, TextAnchor.MiddleRight);
            pointStatus = HudFactory.Label("Point status", pointRect, "", 19, 24, 200, 32, 25, ink);
            HudFactory.Fit(pointStatus, 15);
            pointValue = HudFactory.Label("Point value", pointRect, "", 222, 24, 115, 32, 25, ink, TextAnchor.MiddleRight, true);
            HudFactory.Fit(pointValue, 15);
            pointMeter = HudFactory.Meter("Point meter", pointRect, 19, 57, 318, 7);

            manifestPlate = MakePlate("Cargo Manifest", parent, 129);
            manifestPlate.SetAccent(cargo);
            RectTransform manifestRect = manifestPlate.rectTransform;
            HudFactory.Label("Manifest title", manifestRect, "CARGO MANIFEST", 19, 8, 180, 27, 22, ink);
            handling = HudFactory.Label("Handling penalties", manifestRect, "", 168, 11, 169, 22, 14, cargo, TextAnchor.MiddleRight);
            HudFactory.Rule(manifestRect, 19, 39, 318);
            HudFactory.Label("Item column", manifestRect, "ITEM", 19, 44, 129, 18, 13, muted);
            HudFactory.Label("Quantity column", manifestRect, "QTY", 149, 44, 33, 18, 13, muted, TextAnchor.MiddleRight);
            HudFactory.Label("Mass column", manifestRect, "MASS", 188, 44, 59, 18, 13, muted, TextAnchor.MiddleRight);
            HudFactory.Label("Value column", manifestRect, "VALUE", 255, 44, 82, 18, 13, muted, TextAnchor.MiddleRight);

            for (int i = 0; i < RowsPerPage; i++)
            {
                float y = ManifestRowsTop + i * RowHeight;
                manifestRows[i, 0] = HudFactory.Label("Item", manifestRect, "", 19, y, 126, RowHeight, 18, ink);
                manifestRows[i, 1] = HudFactory.Label("Quantity", manifestRect, "", 148, y, 34, RowHeight, 13, ink, TextAnchor.MiddleRight, true);
                manifestRows[i, 2] = HudFactory.Label("Mass", manifestRect, "", 188, y, 59, RowHeight, 13, ink, TextAnchor.MiddleRight, true);
                manifestRows[i, 3] = HudFactory.Label("Value", manifestRect, "", 252, y, 85, RowHeight, 13, ink, TextAnchor.MiddleRight, true);
                HudFactory.Fit(manifestRows[i, 0], 12);
                HudFactory.Fit(manifestRows[i, 3], 10);
            }

            emptyManifest = HudFactory.Label("Empty hold", manifestRect, "Hold empty", 19, ManifestRowsTop, 250, RowHeight, 17, muted);
            manifestFooter = HudFactory.Label("Manifest controls", manifestRect, "", 19, 0, 318, 22, 14, muted);

            noticePlate = MakePlate("Cargo Notice", parent, NoticeHeight);
            notice = HudFactory.Label("Notice text", noticePlate.rectTransform, "", 19, 5, 318, 25, 18, ink);
            HudFactory.Fit(notice, 12);
            noticeGroup = noticePlate.gameObject.AddComponent<CanvasGroup>();
            Layout(false, false, false, false, false, false);
        }

        /// <summary>
        /// Shows the nearest extraction point: its state, the figure on the right (distance or seconds left),
        /// and the U prompt, which is bright when the uplink can start.
        /// </summary>
        internal void SetPoint(string status, Color color, string value, bool showPrompt, bool promptReady)
        {
            pointStatus.text = status;
            pointStatus.color = color;
            pointPlate.SetAccent(color);
            pointValue.text = value;
            pointKey.gameObject.SetActive(showPrompt);
            pointAction.enabled = showPrompt;
            pointAction.color = promptReady ? HudFactory.Ink : HudFactory.Muted;
        }

        internal void SetPointProgress(float fraction, Color color)
        {
            pointMeter.SetProgress(fraction, color);
        }

        internal void SetHeatBoundaries(float[] values)
        {
            heatMeter.SetBoundaries(values);
        }

        internal void SetHeat(int score, string status, Color statusColor, float fraction)
        {
            heatNumber.text = score.ToString("00");
            tier.text = status;
            tier.color = statusColor;
            heatPlate.SetAccent(statusColor);
            heatMeter.SetProgress(fraction, statusColor);
        }

        /// <summary>Adds the number of Authority ships hunting the player to the heat plate's title, in red.</summary>
        internal void SetHunters(int count)
        {
            heatTitle.text = count > 0
                ? $"AUTHORITY HEAT  <color=#FF795B>· {count} {(count == 1 ? "HUNTER" : "HUNTERS")}</color>"
                : "AUTHORITY HEAT";
        }

        /// <summary>Shows an idle drive and the spool time the current load would need, in seconds.</summary>
        internal void SetJumpReady(float spoolTime)
        {
            jumpStatus.text = "READY";
            jumpStatus.color = HudFactory.Ink;
            jumpTime.text = $"{Mathf.CeilToInt(spoolTime)} s";
            jumpTime.color = HudFactory.Muted;
            jumpAction.text = "SPOOL";
            jumpPlate.SetAccent(HudFactory.JumpColor);
            jumpMeter.SetProgress(0f, HudFactory.JumpColor);
        }

        /// <summary>Shows an idle drive short of fuel: dark matter aboard against what a jump burns, as a filling bar.</summary>
        internal void SetJumpFuel(int aboard, int required)
        {
            jumpStatus.text = "DARK MATTER";
            jumpStatus.color = HudFactory.DarkMatterColor;
            jumpTime.text = $"{aboard} / {required}";
            jumpTime.color = HudFactory.Ink;
            jumpAction.text = "SPOOL";
            jumpPlate.SetAccent(HudFactory.DarkMatterColor);
            jumpMeter.SetProgress(required > 0 ? (float)aboard / required : 1f, HudFactory.DarkMatterColor);
        }

        /// <summary>Shows a spooling drive: its state (SPOOLING, TOO FAST, DISRUPTED) and whole seconds left.</summary>
        internal void SetJumpCountdown(int secondsRemaining, string status, Color statusColor)
        {
            jumpStatus.text = status;
            jumpStatus.color = statusColor;
            jumpPlate.SetAccent(statusColor);
            jumpTime.text = $"{secondsRemaining} s";
            jumpTime.color = HudFactory.Ink;
            jumpAction.text = "CANCEL";
        }

        internal void SetJumpProgress(float fraction, Color color)
        {
            jumpMeter.SetProgress(fraction, color);
        }

        internal void SetCargo(CargoHold hold, bool expanded)
        {
            bool full = hold.LoadFraction >= .9999f;
            cargoTitle.text = full ? "HOLD FULL" : "CARGO HOLD";
            cargoTitle.color = full ? HudFactory.WarningColor : HudFactory.Muted;
            cargoMass.text = $"{hold.TotalMass:0.#}<size=17><color=#9EB6B9> / {hold.MassCapacity:0.#} t</color></size>";
            cargoValue.text = $"{hold.TotalValue:N0} cr";
            manifestToggle.text = expanded ? "CLOSE" : "MANIFEST";
            Color color = full ? HudFactory.WarningColor : HudFactory.CargoColor;
            cargoPlate.SetAccent(color);
            loadMeter.SetProgress(hold.LoadFraction, color);
        }

        internal void SetManifest(CargoHold hold, CargoLoadHandling loadHandling, bool canJettison, int page)
        {
            int start = page * RowsPerPage;
            int count = Mathf.Min(RowsPerPage, hold.Stacks.Count - start);
            int pageCount = Mathf.CeilToInt((float)hold.Stacks.Count / RowsPerPage);
            for (int i = 0; i < RowsPerPage; i++)
            {
                bool visible = i < count;
                for (int column = 0; column < 4; column++)
                    manifestRows[i, column].enabled = visible;

                if (!visible)
                    continue;

                CargoStack stack = hold.Stacks[start + i];
                manifestRows[i, 0].text = stack.Item.DisplayName;
                manifestRows[i, 1].text = stack.Quantity.ToString();
                manifestRows[i, 2].text = $"{stack.Mass:0.#} t";
                manifestRows[i, 3].text = $"{stack.Value:N0} cr";
            }

            emptyManifest.enabled = hold.IsEmpty;
            if (loadHandling != null && loadHandling.isActiveAndEnabled)
            {
                int speed = Mathf.RoundToInt(100f * (1f - loadHandling.CurrentSpeedMultiplier));
                int turn = Mathf.RoundToInt(100f * (1f - loadHandling.CurrentTurnMultiplier));
                handling.text = speed != 0 || turn != 0 ? $"SPEED −{speed}%   TURN −{turn}%" : "";
            }
            else
            {
                handling.text = "";
            }

            string jettison = canJettison ? "[J] Jettison heaviest" : "";
            string pages = pageCount > 1 ? $"[PgUp/PgDn]  {page + 1}/{pageCount}" : "";
            manifestFooter.text = jettison.Length > 0 && pages.Length > 0 ? jettison + "       " + pages : jettison + pages;
            float footer = ManifestRowsTop + Mathf.Max(1, count) * RowHeight + 5;
            HudFactory.SetOffset(manifestFooter.rectTransform, 19, footer);
            manifestPlate.rectTransform.sizeDelta = new Vector2(Width, footer + (manifestFooter.text.Length > 0 ? 28 : 5));
        }

        internal void SetNotice(string message, Color color)
        {
            noticePlate.SetAccent(color);
            notice.text = message;
        }

        internal void SetNoticeOpacity(float alpha)
        {
            noticeGroup.alpha = alpha;
        }

        internal void Layout(bool hasHeat, bool hasCargo, bool hasJumpDrive, bool hasPoint, bool isExpanded, bool hasNotice)
        {
            float y = BottomMargin;
            Stack(cargoPlate, hasCargo, ref y);
            Stack(heatPlate, hasHeat, ref y);
            Stack(jumpPlate, hasJumpDrive, ref y);
            Stack(pointPlate, hasPoint, ref y);
            Stack(manifestPlate, hasCargo && isExpanded, ref y);
            Stack(noticePlate, hasNotice, ref y);
            StackSize = y > BottomMargin ? new Vector2(RightMargin + Width, y - Gutter) : Vector2.zero;
        }

        private static void Stack(HudPlate plate, bool visible, ref float y)
        {
            plate.gameObject.SetActive(visible);
            if (!visible)
                return;

            RectTransform rect = plate.rectTransform;
            rect.anchoredPosition = new Vector2(-RightMargin, y);
            y += rect.sizeDelta.y + Gutter;
        }

        private static HudPlate MakePlate(string name, RectTransform parent, float height)
        {
            return HudFactory.Plate(name, parent, BottomRight, Width, height);
        }
    }
}
