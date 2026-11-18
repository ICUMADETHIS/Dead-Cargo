using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// The loading screen's card in the comic HUD look (phase 6.6 step 4). It has:
    /// - the load's title in small type;
    /// - the destination on a big leaning sticker;
    /// - the bar as an inked sticker bar, with the stage line under it;
    /// - a tip along the bottom of the screen.
    /// The card pops in when shown and the destination sticker sways a little while it waits. Text is only rebuilt when it
    /// changes.
    /// </summary>
    internal sealed class LoadingCardView
    {
        private const float CardWidth = 760f;
        private const float CardHeight = 260f;
        private const float CardTilt = -2f;
        private const float StickerWidth = 700f;
        private const float StickerHeight = 124f;
        private const float BarWidth = 640f;
        private const float TipWidth = 1100f;
        private const float TipHeight = 56f;
        private const float TipBottom = 96f;
        private const float TipTagWidth = 84f;

        // The destination's type: as large as fits the sticker, from a glyph about 0.55 of its size wide.
        private const int MaxDestinationSize = 96;
        private const int MinDestinationSize = 48;
        private const float GlyphWidthShare = 0.55f;

        private const float PopScale = 1.12f;
        private const float PopTime = 0.25f;
        private const float SwayDegrees = 0.6f;
        private const float SwaySpeed = 1.3f;

        private static readonly Color InkText = new Color32(11, 12, 16, 255);
        private static readonly Color TagFace = new Color32(243, 238, 228, 255);
        private static readonly Color Track = new Color32(10, 11, 14, 255);

        private readonly RectTransform card;
        private readonly RectTransform destinationSticker;
        private readonly HudSticker destinationFace;
        private readonly HudSticker bar;
        private readonly Text title;
        private readonly Text destination;
        private readonly Text stage;
        private readonly RectTransform tipRoot;
        private readonly Text tip;

        private float pop;
        private float clock;

        internal LoadingCardView(RectTransform parent)
        {
            card = HudFactory.Anchored("Card", parent, new Vector2(0.5f, 0.5f), CardWidth, CardHeight);
            card.localRotation = Quaternion.Euler(0f, 0f, CardTilt);

            title = HudFactory.Label("Title", card, "", 40f, 0f, CardWidth - 80f, 28f, 22, HudFactory.Muted);
            title.font = HudFactory.StickerFont;
            HudFactory.Unclipped(title);

            // Pivoted at its centre, so it sways about its middle.
            destinationSticker = HudFactory.Place("Destination", card, 20f, 34f, StickerWidth, StickerHeight);
            destinationSticker.pivot = new Vector2(0.5f, 0.5f);
            destinationSticker.anchoredPosition = new Vector2(20f + StickerWidth * 0.5f, -(34f + StickerHeight * 0.5f));
            destinationFace = destinationSticker.gameObject.AddComponent<HudSticker>();
            destinationFace.Configure(HudFactory.JumpColor, 5f, new Vector2(10f, -10f), HudSticker.DefaultSlant);

            // Saira Condensed's line is 1.57 times its size, so the box fits the letters and is left unclipped.
            destination = HudFactory.Label("Name", destinationSticker, "", 30f, 8f, StickerWidth - 60f, StickerHeight - 16f,
                MaxDestinationSize, InkText, TextAnchor.MiddleCenter);
            destination.font = HudFactory.FigureFont;
            destination.fontStyle = FontStyle.Italic;
            destination.horizontalOverflow = HorizontalWrapMode.Overflow;
            HudFactory.Unclipped(destination);

            RectTransform barRect = HudFactory.Place("Bar", card, 36f, 184f, BarWidth, 26f);
            bar = barRect.gameObject.AddComponent<HudSticker>();
            bar.Configure(HudFactory.JumpColor, 3f, new Vector2(6f, -6f), HudSticker.DefaultSlant);
            bar.SetTrack(Track);
            bar.SetFill(0f);

            stage = HudFactory.Label("Stage", card, "", 44f, 220f, BarWidth, 24f, 18, HudFactory.Muted);
            stage.font = HudFactory.StickerFont;
            HudFactory.Unclipped(stage);

            tipRoot = HudFactory.Anchored("Tip", parent, new Vector2(0.5f, 0f), TipWidth, TipHeight);
            tipRoot.anchoredPosition = new Vector2(0f, TipBottom);

            RectTransform tag = HudFactory.Place("Tag", tipRoot, 0f, 10f, TipTagWidth, 34f);
            tag.gameObject.AddComponent<HudSticker>().Configure(TagFace, 3f, new Vector2(5f, -5f), HudSticker.DefaultSlant);
            Text tagText = HudFactory.Label("Tag Text", tag, "TIP", 8f, 2f, TipTagWidth - 12f, 30f, 18, InkText, TextAnchor.MiddleCenter);
            tagText.font = HudFactory.StickerFont;
            HudFactory.Unclipped(tagText);

            tip = HudFactory.Label("Tip Text", tipRoot, "", TipTagWidth + 24f, 0f, TipWidth - TipTagWidth - 24f, TipHeight, 20, HudFactory.Ink);
            tip.font = HudFactory.StickerFont;
            tip.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        /// <summary>Fills the card for a load and pops it in. An empty tip hides the tip line.</summary>
        internal void Show(string loadTitle, string destinationName, Color accent, string tipText)
        {
            title.text = loadTitle;
            destination.text = destinationName;
            destination.fontSize = DestinationSize(destinationName);
            destinationFace.SetFace(accent);
            bar.SetFace(accent);
            bar.SetFill(0f);

            bool hasTip = !string.IsNullOrEmpty(tipText);
            tipRoot.gameObject.SetActive(hasTip);
            if (hasTip)
                tip.text = tipText;

            pop = 1f;
            clock = 0f;
        }

        /// <summary>Names the stage under the bar, such as LOADING or PAINTING SKY.</summary>
        internal void SetStage(string text)
        {
            stage.text = text;
        }

        /// <summary>The bar's fill, 0 to 1. The loading screen only ever moves it forward.</summary>
        internal void SetProgress(float share)
        {
            bar.SetFill(share);
        }

        /// <summary>Moves the pop and the sway on. Unscaled time, since a load may start with the game paused.</summary>
        internal void Tick(float deltaTime)
        {
            clock += deltaTime;
            pop = Mathf.Max(0f, pop - deltaTime / PopTime);
            float scale = 1f + (PopScale - 1f) * pop * pop;
            card.localScale = new Vector3(scale, scale, 1f);
            destinationSticker.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(clock * SwaySpeed) * SwayDegrees);
        }

        private static int DestinationSize(string text)
        {
            int length = Mathf.Max(1, text.Length);
            return Mathf.Clamp(Mathf.FloorToInt((StickerWidth - 60f) / (length * GlyphWidthShare)), MinDestinationSize, MaxDestinationSize);
        }
    }
}
