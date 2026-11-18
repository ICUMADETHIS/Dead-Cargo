using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// A marker's label: a leaning sticker holding the target's name, an optional state and its distance, sized to its text.
    /// Loud labels are the marker's colour with ink text; quiet ones are charcoal with the name in the marker's colour.
    /// Text and size are only rebuilt when they change. The root is centred on the point the layer places it at.
    /// </summary>
    internal sealed class HudMarkerLabel
    {
        // Lean for each unit of height, tan 12°, a touch less than the strip's.
        private const float Slant = 0.21f;

        private static readonly Color InkText = new Color32(11, 12, 16, 255);
        private static readonly Color DetailText = new Color32(11, 12, 16, 180);
        private static readonly Color QuietFace = new Color32(20, 22, 28, 235);
        private static readonly Color QuietFigure = new Color32(201, 214, 216, 255);

        private readonly RectTransform root;
        private readonly HudSticker body;
        private readonly Text name;
        private readonly Text detail;
        private readonly Text figure;

        private HudMarkerLook look = HudMarkerLook.Plain;
        private Color color;
        private string shownName;
        private string shownDetail;
        private string shownFigure;

        internal HudMarkerLabel(RectTransform parent)
        {
            root = HudFactory.Anchored("Label", parent, new Vector2(0.5f, 0.5f), 0f, 0f);
            body = HudFactory.Place("Body", root, 0f, 0f, 0f, 0f).gameObject.AddComponent<HudSticker>();
            name = Line("Name", HudFactory.StickerFont);
            detail = Line("State", HudFactory.StickerFont);
            figure = Line("Figure", HudFactory.FigureFont);
            figure.fontStyle = FontStyle.Italic;
            root.gameObject.SetActive(false);
        }

        /// <summary>The label's size, shadow excluded, in reference pixels.</summary>
        internal Vector2 Size => root.sizeDelta;

        internal void SetVisible(bool visible)
        {
            if (root.gameObject.activeSelf != visible)
                root.gameObject.SetActive(visible);
        }

        internal void SetPosition(Vector2 centre)
        {
            root.anchoredPosition = centre;
        }

        /// <summary>The label's look, colour and text; a null or empty state is left out.</summary>
        internal void Set(HudMarkerLook labelLook, Color tint, string nameText, string detailText, string figureText)
        {
            bool restyle = labelLook != look;
            if (restyle || tint != color)
            {
                look = labelLook;
                color = tint;
                Paint();
            }

            bool retext = nameText != shownName || detailText != shownDetail || figureText != shownFigure;
            if (!restyle && !retext)
                return;

            shownName = nameText;
            shownDetail = detailText;
            shownFigure = figureText;
            name.text = nameText ?? "";
            detail.text = look == HudMarkerLook.Loud ? detailText ?? "" : "";
            figure.text = figureText ?? "";
            Layout();
        }

        private void Paint()
        {
            bool loud = look == HudMarkerLook.Loud;
            Color face = loud ? color : QuietFace;
            face.a = loud ? 1f : QuietFace.a;
            body.Configure(face, loud ? 3f : 2f, loud ? new Vector2(4f, -4f) : new Vector2(3f, -3f), Slant);
            name.color = loud ? InkText : color;
            detail.color = DetailText;
            figure.color = loud ? InkText : QuietFigure;
            name.fontSize = loud ? 12 : 11;
            detail.fontSize = 11;
            figure.fontSize = loud ? 17 : 14;
        }

        // Name, state and figure side by side, the sticker wrapped round them with room for its lean.
        private void Layout()
        {
            bool loud = look == HudMarkerLook.Loud;
            float height = loud ? 28f : 22f;
            float padding = loud ? 9f : 7f;
            float gap = loud ? 7f : 6f;
            float outline = loud ? 3f : 2f;
            float lean = height * Slant;

            float x = outline + padding + lean * 0.5f;
            x = PlaceLine(name, x, height, gap);
            x = PlaceLine(detail, x, height, gap);
            x = PlaceLine(figure, x, height, gap);
            float width = Mathf.Ceil(x - gap + padding + outline + lean * 0.5f);

            root.sizeDelta = new Vector2(width, height);
            body.rectTransform.sizeDelta = root.sizeDelta;
        }

        // Places a line of text at x, centred on the sticker's height. Returns where the next one starts.
        private static float PlaceLine(Text text, float x, float height, float gap)
        {
            bool shown = text.text.Length > 0;
            if (text.enabled != shown)
                text.enabled = shown;

            if (!shown)
                return x;

            float width = Mathf.Ceil(text.preferredWidth);
            RectTransform rect = text.rectTransform;
            rect.sizeDelta = new Vector2(width, height);
            HudFactory.SetOffset(rect, x, 0f);
            return x + width + gap;
        }

        private Text Line(string objectName, Font font)
        {
            Text text = HudFactory.Label(objectName, root, "", 0f, 0f, 0f, 0f, 12, InkText);
            text.font = font;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            HudFactory.Unclipped(text);
            return text;
        }
    }
}
