using DeadCargo.Cargo;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// The run HUD as pop-up tags (<see cref="HudTag"/>) stacked up the screen's right edge, above the status strip, each
    /// shown only while it matters: cargo when it changes or the hold is full, heat while there is any, the jump drive while
    /// it is ready, spooling or taking on fuel, the extraction point near or in use, cruise while a destination is ahead,
    /// and notices on top. The manifest is an inked panel on the left while it is open. The stack eases as tags come and go.
    /// </summary>
    internal sealed class ExtractionHudView
    {
        internal const int RowsPerPage = 5;

        private const float TagWidth = 340f;
        private const float NoticeWidth = 520f;
        private const float Gap = 14f;
        private const float RightMargin = 40f;

        // The stack starts this far above the band the status strip covers, in reference pixels.
        private const float StackLift = 180f;

        private const float PulseTime = 3.5f;
        private const float ManifestWidth = 390f;
        private const float ManifestLeft = 40f;
        private const float ManifestLift = 30f;
        private const float ManifestSlant = 0.06f;
        private const float ManifestRowsTop = 74f;
        private const float RowHeight = 24f;

        private static readonly Color PanelFace = new Color32(24, 27, 34, 245);

        private readonly HudTag cargoTag;
        private readonly HudTag heatTag;
        private readonly HudTag jumpTag;
        private readonly HudTag pointTag;
        private readonly HudTag cruiseTag;
        private readonly HudTag noticeTag;
        private readonly HudTag[] stack;

        private readonly RectTransform manifest;
        private readonly HudSticker manifestBody;
        private readonly Text handling;
        private readonly Text emptyManifest;
        private readonly Text manifestFooter;
        private readonly Text[,] manifestRows = new Text[RowsPerPage, 4];

        private string heatStatus = "";
        private Color heatColor = HudFactory.Muted;
        private int heatScore;
        private int hunters;
        private float heatFraction;

        private string cargoHeadline;
        private float cargoHeadlineLeft;
        private string cargoMass = "";
        private string cargoValue = "";
        private bool cargoFull;
        private bool manifestOpen;

        private string jumpHeadline;
        private float jumpHeadlineLeft;
        private string jumpStatus = "";
        private string jumpFigure = "";
        private Color jumpColor = HudFactory.JumpColor;

        /// <summary>
        /// Size of the area up the screen's right edge the shown tags cover, from the bottom, in reference pixels. Zero when
        /// no tag is shown. Updated by <see cref="Tick"/>.
        /// </summary>
        internal Vector2 StackSize { get; private set; }

        internal ExtractionHudView(RectTransform parent)
        {
            cargoTag = new HudTag(parent, "Cargo Tag", TagWidth, "I");
            heatTag = new HudTag(parent, "Heat Tag", TagWidth, null);
            jumpTag = new HudTag(parent, "Jump Tag", TagWidth, "H");
            pointTag = new HudTag(parent, "Extraction Tag", TagWidth, "U");
            cruiseTag = new HudTag(parent, "Cruise Tag", TagWidth, "B");
            noticeTag = new HudTag(parent, "Notice Tag", NoticeWidth, null);
            stack = new[] { cargoTag, heatTag, jumpTag, pointTag, cruiseTag, noticeTag };

            manifest = HudFactory.Anchored("Cargo Manifest", parent, Vector2.zero, ManifestWidth, 160f);
            manifestBody = manifest.gameObject.AddComponent<HudSticker>();
            manifestBody.Configure(PanelFace, 4f, new Vector2(8f, -8f), ManifestSlant);

            Text title = HudFactory.Label("Manifest Title", manifest, "CARGO MANIFEST", 22f, 10f, 220f, 30f, 24, HudFactory.CargoColor);
            title.font = HudFactory.FigureFont;
            title.fontStyle = FontStyle.Italic;
            handling = Caption("Handling Penalties", manifest, "", 200f, 16f, 168f, 20f, 12, HudFactory.CargoColor, TextAnchor.MiddleRight);
            Caption("Item Column", manifest, "ITEM", 24f, 48f, 140f, 18f, 11, HudFactory.Muted, TextAnchor.MiddleLeft);
            Caption("Quantity Column", manifest, "QTY", 170f, 48f, 34f, 18f, 11, HudFactory.Muted, TextAnchor.MiddleRight);
            Caption("Mass Column", manifest, "MASS", 208f, 48f, 62f, 18f, 11, HudFactory.Muted, TextAnchor.MiddleRight);
            Caption("Value Column", manifest, "VALUE", 276f, 48f, 92f, 18f, 11, HudFactory.Muted, TextAnchor.MiddleRight);

            for (int i = 0; i < RowsPerPage; i++)
            {
                float y = ManifestRowsTop + i * RowHeight;
                manifestRows[i, 0] = Caption("Item", manifest, "", 24f, y, 144f, RowHeight, 14, HudFactory.Ink, TextAnchor.MiddleLeft);
                manifestRows[i, 1] = Numbers("Quantity", manifest, 170f, y, 34f);
                manifestRows[i, 2] = Numbers("Mass", manifest, 208f, y, 62f);
                manifestRows[i, 3] = Numbers("Value", manifest, 276f, y, 92f);
                HudFactory.Fit(manifestRows[i, 0], 11);
                HudFactory.Fit(manifestRows[i, 3], 11);
            }

            emptyManifest = Caption("Empty Hold", manifest, "HOLD EMPTY", 24f, ManifestRowsTop, 250f, RowHeight, 14, HudFactory.Muted,
                TextAnchor.MiddleLeft);
            manifestFooter = Caption("Manifest Controls", manifest, "", 24f, 0f, 344f, 20f, 12, HudFactory.Muted, TextAnchor.MiddleLeft);
            manifest.gameObject.SetActive(false);
        }

        /// <summary>
        /// The nearest extraction point, shown while it is in range or in use: its state, the figure on the right (distance
        /// or seconds left), the U key while an uplink can start, and its progress.
        /// </summary>
        internal void SetPoint(bool wanted, string status, string figure, Color color, bool showKey, float progress)
        {
            pointTag.SetHeld(wanted);
            pointTag.SetContent("EXTRACTION POINT", status, figure, color);
            pointTag.SetKeyVisible(showKey);
            pointTag.SetProgress(progress);
        }

        internal void SetPointProgress(float fraction)
        {
            pointTag.SetProgress(fraction);
        }

        internal void SetHeat(int score, string status, Color statusColor, float fraction)
        {
            heatScore = score;
            heatStatus = status;
            heatColor = statusColor;
            heatFraction = fraction;
            ShowHeat();
        }

        /// <summary>The number of Authority ships hunting the player, named on the heat tag. Any keeps the tag on screen.</summary>
        internal void SetHunters(int count)
        {
            hunters = count;
            ShowHeat();
        }

        /// <summary>An idle drive with fuel: ready, with the spool time the current load would need, in seconds.</summary>
        internal void SetJumpReady(float spoolTime)
        {
            ShowJump("READY", $"{Mathf.CeilToInt(spoolTime)} S", HudFactory.JumpColor, -1f, true);
        }

        /// <summary>An idle drive short of fuel: dark matter aboard against what a jump burns. Shown when it changes.</summary>
        internal void SetJumpFuel(int aboard, int required)
        {
            ShowJump("DARK MATTER", $"{aboard} / {required}", HudFactory.DarkMatterColor, required > 0 ? (float)aboard / required : 1f, false);
        }

        /// <summary>A spooling drive: its state (SPOOLING, TOO FAST, DISRUPTED) and whole seconds left.</summary>
        internal void SetJumpCountdown(int secondsRemaining, string status, Color statusColor)
        {
            ShowJump(status, $"{secondsRemaining} S", statusColor, 0f, true);
        }

        internal void SetJumpProgress(float fraction, Color color)
        {
            jumpTag.SetProgress(fraction);
        }

        /// <summary>Shows the jump tag for a while, headed by what just happened, such as dark matter taken aboard.</summary>
        internal void PulseJump(string headline)
        {
            jumpHeadline = headline;
            jumpHeadlineLeft = PulseTime;
            jumpTag.Pulse(PulseTime);
            jumpTag.SetContent(jumpHeadline, jumpStatus, jumpFigure, jumpColor);
        }

        /// <summary>
        /// The cruise drive, shown while it matters: its state, the figure on the right (the destination ahead, seconds of
        /// charge, speed or height), what B does now, and the meter.
        /// </summary>
        internal void SetCruise(bool wanted, string status, Color statusColor, string value, string action, float fraction)
        {
            cruiseTag.SetHeld(wanted);
            cruiseTag.SetContent(string.IsNullOrEmpty(action) ? "CRUISE DRIVE" : $"CRUISE  ·  {action}", status, value, statusColor);
            cruiseTag.SetKeyVisible(!string.IsNullOrEmpty(action));
            cruiseTag.SetProgress(fraction > 0f ? fraction : -1f);
        }

        internal void SetCruiseProgress(float fraction, Color color)
        {
            cruiseTag.SetProgress(fraction > 0f ? fraction : -1f);
        }

        /// <summary>The hold: shown while full or while the manifest is open, and for a while after it changes.</summary>
        internal void SetCargo(CargoHold hold, bool expanded)
        {
            cargoFull = hold.LoadFraction >= .9999f;
            cargoMass = $"{hold.TotalMass:0.#} / {hold.MassCapacity:0.#} T";
            cargoValue = $"{hold.TotalValue:N0} CR";
            manifestOpen = expanded;
            cargoTag.SetHeld(cargoFull || expanded);
            cargoTag.SetProgress(hold.LoadFraction);
            ShowCargo();
        }

        /// <summary>Shows the cargo tag for a while, headed by what just happened, such as cargo taken aboard.</summary>
        internal void PulseCargo(string headline)
        {
            cargoHeadline = headline;
            cargoHeadlineLeft = PulseTime;
            cargoTag.Pulse(PulseTime);
            ShowCargo();
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
                manifestRows[i, 0].text = stack.Item.DisplayName.ToUpperInvariant();
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

            string jettison = canJettison ? "[J] JETTISON HEAVIEST" : "";
            string pages = pageCount > 1 ? $"[PGUP/PGDN]  {page + 1}/{pageCount}" : "";
            manifestFooter.text = jettison.Length > 0 && pages.Length > 0 ? jettison + "       " + pages : jettison + pages;
            float footer = ManifestRowsTop + Mathf.Max(1, count) * RowHeight + 6f;
            HudFactory.SetOffset(manifestFooter.rectTransform, 24f, footer);
            manifest.sizeDelta = new Vector2(ManifestWidth, footer + (manifestFooter.text.Length > 0 ? 30f : 8f));
        }

        internal void SetNotice(string message, Color color)
        {
            noticeTag.SetContent(null, message, null, color);
        }

        /// <summary>Which systems the HUD has, whether the manifest is open, and whether a notice is up.</summary>
        internal void Layout(bool hasHeat, bool hasCargo, bool hasJumpDrive, bool hasCruise, bool hasPoint, bool isExpanded, bool hasNotice)
        {
            heatTag.SetAvailable(hasHeat);
            cargoTag.SetAvailable(hasCargo);
            jumpTag.SetAvailable(hasJumpDrive);
            cruiseTag.SetAvailable(hasCruise);
            pointTag.SetAvailable(hasPoint);
            noticeTag.SetHeld(hasNotice);

            bool showManifest = hasCargo && isExpanded;
            if (manifest.gameObject.activeSelf != showManifest)
                manifest.gameObject.SetActive(showManifest);
        }

        /// <summary>
        /// Moves the tags for this frame, the stack starting above the band of the screen's bottom the status strip covers,
        /// <paramref name="coveredBottom"/> reference pixels high.
        /// </summary>
        internal void Tick(float deltaTime, float coveredBottom)
        {
            if (cargoHeadline != null && (cargoHeadlineLeft -= deltaTime) <= 0f)
            {
                cargoHeadline = null;
                ShowCargo();
            }

            if (jumpHeadline != null && (jumpHeadlineLeft -= deltaTime) <= 0f)
            {
                jumpHeadline = null;
                jumpTag.SetContent("JUMP DRIVE", jumpStatus, jumpFigure, jumpColor);
            }

            manifest.anchoredPosition = new Vector2(ManifestLeft, coveredBottom + ManifestLift);

            float y = coveredBottom + StackLift;
            float widest = 0f;
            foreach (HudTag tag in stack)
            {
                tag.Tick(deltaTime, y, RightMargin);
                if (tag.Shown <= 0f)
                    continue;

                y += (HudTag.Height + Gap) * tag.Shown;
                widest = Mathf.Max(widest, tag.Width);
            }

            StackSize = widest > 0f ? new Vector2(RightMargin + widest, y) : Vector2.zero;
        }

        private void ShowHeat()
        {
            string label = hunters > 0 ? $"AUTHORITY  ·  {hunters} {(hunters == 1 ? "HUNTER" : "HUNTERS")}" : "AUTHORITY HEAT";
            Color color = hunters > 0 && heatScore <= 0 ? HudFactory.WarningColor : heatColor;
            heatTag.SetHeld(heatScore > 0 || hunters > 0);
            heatTag.SetContent(label, heatStatus, heatScore.ToString("00"), color);
            heatTag.SetProgress(heatFraction);
        }

        private void ShowJump(string status, string figure, Color color, float progress, bool held)
        {
            jumpStatus = status;
            jumpFigure = figure;
            jumpColor = color;
            jumpTag.SetHeld(held);
            jumpTag.SetContent(jumpHeadline ?? "JUMP DRIVE", status, figure, color);
            jumpTag.SetProgress(progress);
        }

        private void ShowCargo()
        {
            string label = cargoHeadline ?? (cargoFull ? "HOLD FULL" : manifestOpen ? "CARGO  ·  CLOSE" : "CARGO HOLD");
            cargoTag.SetContent(label, cargoMass, cargoValue, cargoFull ? HudFactory.WarningColor : HudFactory.CargoColor);
        }

        private static Text Caption(string name, RectTransform parent, string content, float x, float y, float width, float height, int size,
            Color tint, TextAnchor alignment)
        {
            Text text = HudFactory.Label(name, parent, content, x, y, width, height, size, tint, alignment);
            text.font = HudFactory.StickerFont;
            return text;
        }

        private static Text Numbers(string name, RectTransform parent, float x, float y, float width)
        {
            Text text = HudFactory.Label(name, parent, "", x, y, width, RowHeight, 16, HudFactory.Ink, TextAnchor.MiddleRight);
            text.font = HudFactory.FigureFont;
            return text;
        }
    }
}
