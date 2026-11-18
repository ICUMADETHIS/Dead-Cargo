using DeadCargo.Cargo;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// The run HUD as pop-up tags (<see cref="HudTag"/>) stacked up the screen's right edge, above the status strip, each
    /// shown only while it matters: cargo when it changes or the hold is full, heat while there is any, the jump drive while
    /// it is ready, spooling or taking on fuel, the extraction point near or in use, cruise while a destination is ahead,
    /// the ship's own prompts (such as launching) while they apply, and notices on top. The stack eases as tags come and go.
    /// The hold itself opens as its own panel (<see cref="CargoHoldPanel"/>).
    /// </summary>
    internal sealed class ExtractionHudView
    {
        private const float TagWidth = 340f;
        private const float NoticeWidth = 520f;
        private const float Gap = 14f;
        private const float RightMargin = 40f;

        // The stack starts this far above the band the status strip covers, in reference pixels.
        private const float StackLift = 180f;

        private const float PulseTime = 3.5f;

        private const string PromptLabel = "SHIP";

        private readonly HudTag cargoTag;
        private readonly HudTag heatTag;
        private readonly HudTag jumpTag;
        private readonly HudTag pointTag;
        private readonly HudTag cruiseTag;
        private readonly HudTag promptTag;
        private readonly HudTag noticeTag;
        private readonly HudTag[] stack;

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
        private bool holdOpen;

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
            promptTag = new HudTag(parent, "Prompt Tag", TagWidth, "");
            noticeTag = new HudTag(parent, "Notice Tag", NoticeWidth, null);
            stack = new[] { cargoTag, heatTag, jumpTag, pointTag, cruiseTag, promptTag, noticeTag };
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

        /// <summary>
        /// The ship's own prompt, such as launching from a pad: the key to press, or null when it names none, and what the
        /// key does. Held while <paramref name="shown"/>.
        /// </summary>
        internal void SetPrompt(bool shown, string key, string action)
        {
            promptTag.SetHeld(shown);
            if (!shown)
                return;

            promptTag.SetKeyName(key);
            promptTag.SetKeyVisible(!string.IsNullOrEmpty(key));
            promptTag.SetContent(PromptLabel, action, null, StatusStripView.Figure);
            promptTag.SetProgress(-1f);
        }

        /// <summary>The hold: shown while full or while the hold panel is open, and for a while after it changes.</summary>
        internal void SetCargo(CargoHold hold, bool panelOpen)
        {
            cargoFull = hold.LoadFraction >= .9999f;
            cargoMass = $"{hold.TotalMass:0.#} / {hold.MassCapacity:0.#} T";
            cargoValue = $"{hold.TotalValue:N0} CR";
            holdOpen = panelOpen;
            cargoTag.SetHeld(cargoFull || panelOpen);
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

        internal void SetNotice(string message, Color color)
        {
            noticeTag.SetContent(null, message, null, color);
        }

        /// <summary>Which systems the HUD has, and whether a notice is up.</summary>
        internal void Layout(bool hasHeat, bool hasCargo, bool hasJumpDrive, bool hasCruise, bool hasPoint, bool hasNotice)
        {
            heatTag.SetAvailable(hasHeat);
            cargoTag.SetAvailable(hasCargo);
            jumpTag.SetAvailable(hasJumpDrive);
            cruiseTag.SetAvailable(hasCruise);
            pointTag.SetAvailable(hasPoint);
            noticeTag.SetHeld(hasNotice);
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
            string label = cargoHeadline ?? (cargoFull ? "HOLD FULL" : holdOpen ? "CARGO  ·  CLOSE" : "CARGO HOLD");
            cargoTag.SetContent(label, cargoMass, cargoValue, cargoFull ? HudFactory.WarningColor : HudFactory.CargoColor);
        }
    }
}
