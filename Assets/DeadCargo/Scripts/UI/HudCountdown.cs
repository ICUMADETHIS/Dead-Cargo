using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// The big countdown at the top centre for the last seconds that matter (phase 6.17): the jump spool, or an extraction
    /// point's window about to close. A leaning sticker with a small label over a big figure, which pops as each second
    /// ticks. Silent. Text is only rebuilt when the second or the label changes.
    /// </summary>
    internal sealed class HudCountdown
    {
        private const float Width = 240f;
        private const float Height = 136f;
        private const float Top = 96f;
        private const float Tilt = -2f;
        private const float PopScale = 1.3f;
        private const float PopTime = 0.2f;
        private const float FadeTime = 0.15f;

        private static readonly Color InkText = new Color32(11, 12, 16, 255);

        private readonly RectTransform root;
        private readonly CanvasGroup group;
        private readonly HudSticker body;
        private readonly Text label;
        private readonly Text figure;

        private bool wanted;
        private float shown;
        private float pop;
        private int shownSeconds = -1;
        private string shownLabel;

        internal HudCountdown(RectTransform parent)
        {
            root = HudFactory.Anchored("Countdown", parent, new Vector2(0.5f, 1f), Width, Height);
            root.anchoredPosition = new Vector2(0f, -Top);
            root.localRotation = Quaternion.Euler(0f, 0f, Tilt);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            body = HudFactory.Place("Body", root, 0f, 0f, Width, Height).gameObject.AddComponent<HudSticker>();
            body.Configure(HudFactory.JumpColor, 4f, new Vector2(8f, -8f), HudSticker.DefaultSlant);

            label = HudFactory.Label("Label", root, "", 24f, 10f, Width - 30f, 20f, 16, InkText, TextAnchor.MiddleCenter);
            label.font = HudFactory.StickerFont;
            HudFactory.Unclipped(label);

            // Saira Condensed's line is 1.57 times its size, so the box is sized to the letters and left unclipped.
            figure = HudFactory.Label("Seconds", root, "", 24f, 26f, Width - 30f, 100f, 88, InkText, TextAnchor.MiddleCenter);
            figure.font = HudFactory.FigureFont;
            figure.fontStyle = FontStyle.Italic;
            figure.horizontalOverflow = HorizontalWrapMode.Overflow;
            HudFactory.Unclipped(figure);

            root.gameObject.SetActive(false);
        }

        /// <summary>Holds the countdown on screen this frame with a label, whole seconds left and the sticker's colour.</summary>
        internal void Show(string text, int seconds, Color face)
        {
            wanted = true;
            body.SetFace(face);
            if (!ReferenceEquals(text, shownLabel))
            {
                shownLabel = text;
                label.text = text;
            }

            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                figure.text = seconds.ToString();
                pop = 1f;
            }
        }

        /// <summary>Lets the countdown go. The next one to show pops from its first second.</summary>
        internal void Hide()
        {
            wanted = false;
            shownSeconds = -1;
        }

        internal void Tick(float deltaTime)
        {
            shown = Mathf.MoveTowards(shown, wanted ? 1f : 0f, deltaTime / FadeTime);
            bool visible = shown > 0f;
            if (root.gameObject.activeSelf != visible)
                root.gameObject.SetActive(visible);
            if (!visible)
                return;

            pop = Mathf.Max(0f, pop - deltaTime / PopTime);
            float scale = 1f + (PopScale - 1f) * pop * pop;
            root.localScale = new Vector3(scale, scale, 1f);
            group.alpha = shown;
        }
    }
}
