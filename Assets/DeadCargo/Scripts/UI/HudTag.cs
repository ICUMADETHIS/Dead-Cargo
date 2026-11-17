using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// One pop-up tag of the run HUD: a leaning colour sticker with an ink outline and hard shadow, holding an optional key,
    /// a small label, a big value, a figure on the right and a thin progress bar. It is shown while it is wanted: held on,
    /// or for a while after a <see cref="Pulse"/>. It slides in from the screen's right edge and back out, and its stack
    /// moves it up or down smoothly. Text is only rebuilt when it changes.
    /// </summary>
    internal sealed class HudTag
    {
        internal const float Height = 58f;

        private const float SlideTime = 0.18f;
        private const float MoveRate = 14f;
        private const float SlideClearance = 60f;
        private const float Tilt = 1.5f;
        private const float KeySize = 30f;

        private static readonly Color InkText = new Color32(11, 12, 16, 255);
        private static readonly Color ProgressTrack = new Color(0f, 0f, 0f, 0.22f);

        private readonly RectTransform root;
        private readonly CanvasGroup group;
        private readonly HudSticker body;
        private readonly HudSticker progress;
        private readonly Text label;
        private readonly Text value;
        private readonly Text figure;
        private readonly RectTransform key;
        private readonly float width;

        private float shown;
        private float pulseLeft;
        private bool held;
        private bool available = true;
        private float y = float.NaN;
        private string shownLabel;
        private string shownValue;
        private string shownFigure;

        internal HudTag(RectTransform parent, string name, float tagWidth, string keyName)
        {
            width = tagWidth;
            root = HudFactory.Anchored(name, parent, new Vector2(1f, 0f), width, Height);
            root.localRotation = Quaternion.Euler(0f, 0f, Tilt);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            RectTransform bodyRect = HudFactory.Place("Body", root, 0f, 0f, width, Height);
            body = bodyRect.gameObject.AddComponent<HudSticker>();
            body.Configure(HudFactory.CruiseColor, 3f, new Vector2(6f, -6f), HudSticker.DefaultSlant);

            float left = 18f;
            if (keyName != null)
            {
                key = HudFactory.Place("Key", root, 18f, (Height - KeySize) * 0.5f, KeySize, KeySize);
                key.gameObject.AddComponent<Image>().color = InkText;
                Text keyText = HudFactory.Label("Key Name", key, keyName, 0f, 0f, KeySize, KeySize, 20, StatusStripView.Figure,
                    TextAnchor.MiddleCenter);
                keyText.font = HudFactory.FigureFont;
                left = 18f + KeySize + 10f;
            }

            float textWidth = width - left - 22f;
            label = HudFactory.Label("Label", root, "", left, 6f, textWidth, 16f, 12, InkText);
            label.font = HudFactory.StickerFont;
            value = HudFactory.Label("Value", root, "", left, 19f, textWidth, 32f, 25, InkText);
            value.font = HudFactory.FigureFont;
            value.fontStyle = FontStyle.Italic;
            HudFactory.Fit(value, 14);
            figure = HudFactory.Label("Figure", root, "", width - 150f, 12f, 128f, 34f, 25, InkText, TextAnchor.MiddleRight);
            figure.font = HudFactory.FigureFont;
            figure.fontStyle = FontStyle.Italic;
            figure.horizontalOverflow = HorizontalWrapMode.Overflow;

            progress = HudFactory.Place("Progress", root, left, Height - 12f, width - left - 30f, 5f).gameObject.AddComponent<HudSticker>();
            progress.Configure(InkText, 0f, Vector2.zero, HudSticker.DefaultSlant);
            progress.SetTrack(ProgressTrack);
            progress.gameObject.SetActive(false);

            root.gameObject.SetActive(false);
        }

        /// <summary>True while the tag wants to be on screen.</summary>
        internal bool Wanted => available && (held || pulseLeft > 0f);

        /// <summary>How far in the tag has slid, 0 gone to 1 in place.</summary>
        internal float Shown => shown;

        internal float Width => width;

        /// <summary>The tag's text. A null or empty label leaves the value centred on its own.</summary>
        internal void SetContent(string labelText, string valueText, string figureText, Color face)
        {
            body.SetFace(face);
            if (labelText != shownLabel)
            {
                shownLabel = labelText;
                label.text = labelText ?? "";
                bool hasLabel = !string.IsNullOrEmpty(labelText);
                HudFactory.SetOffset(value.rectTransform, value.rectTransform.anchoredPosition.x, hasLabel ? 19f : 12f);
            }

            if (valueText != shownValue)
            {
                shownValue = valueText;
                value.text = valueText ?? "";
            }

            if (figureText != shownFigure)
            {
                shownFigure = figureText;
                figure.text = figureText ?? "";
            }
        }

        /// <summary>The thin bar along the tag's foot, 0 to 1; negative hides it.</summary>
        internal void SetProgress(float fraction)
        {
            bool visible = fraction >= 0f;
            if (progress.gameObject.activeSelf != visible)
                progress.gameObject.SetActive(visible);
            if (visible)
                progress.SetFill(fraction);
        }

        /// <summary>Keeps the tag on screen until released.</summary>
        internal void SetHeld(bool on)
        {
            held = on;
        }

        /// <summary>Shows the tag for <paramref name="seconds"/>, or longer while it is held.</summary>
        internal void Pulse(float seconds)
        {
            pulseLeft = Mathf.Max(pulseLeft, seconds);
        }

        /// <summary>Shows or hides the key cap.</summary>
        internal void SetKeyVisible(bool visible)
        {
            if (key != null && key.gameObject.activeSelf != visible)
                key.gameObject.SetActive(visible);
        }

        /// <summary>Ends a pulse at once.</summary>
        internal void EndPulse()
        {
            pulseLeft = 0f;
        }

        /// <summary>False when the system the tag shows is missing, so it never shows.</summary>
        internal void SetAvailable(bool on)
        {
            available = on;
        }

        /// <summary>Slides the tag in or out and moves it towards its place in the stack, <paramref name="targetY"/> above the bottom.</summary>
        internal void Tick(float deltaTime, float targetY, float rightMargin)
        {
            pulseLeft = Mathf.Max(0f, pulseLeft - deltaTime);
            shown = Mathf.MoveTowards(shown, Wanted ? 1f : 0f, deltaTime / SlideTime);
            bool visible = shown > 0f;
            if (root.gameObject.activeSelf != visible)
                root.gameObject.SetActive(visible);
            if (!visible)
            {
                y = float.NaN;
                return;
            }

            y = float.IsNaN(y) ? targetY : Mathf.Lerp(y, targetY, 1f - Mathf.Exp(-MoveRate * deltaTime));
            float eased = 1f - (1f - shown) * (1f - shown);
            root.anchoredPosition = new Vector2(-rightMargin + (1f - eased) * (width + SlideClearance), y);
            group.alpha = eased;
        }
    }
}
