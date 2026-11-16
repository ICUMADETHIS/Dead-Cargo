using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// The HUD palette and builders for its shared elements.
    /// Positions are in reference pixels, measured from the parent's top-left corner.
    /// </summary>
    internal static class HudFactory
    {
        internal static readonly Color Ink = new Color32(234, 242, 241, 255);
        internal static readonly Color Muted = new Color32(158, 182, 185, 255);
        internal static readonly Color CargoColor = new Color32(239, 188, 112, 255);
        internal static readonly Color WarningColor = new Color32(255, 121, 91, 255);
        internal static readonly Color JumpColor = new Color32(116, 206, 255, 255);
        internal static readonly Color SuccessColor = new Color32(124, 226, 158, 255);
        internal static readonly Color DarkMatterColor = new Color32(178, 136, 255, 255);

        private const string HeadingFontPath = "HUD/BarlowCondensed-SemiBold";
        private const string NumericFontPath = "HUD/IBMPlexMono-Regular";
        private const string FallbackFontName = "LegacyRuntime.ttf";
        private const float KeySize = 18f;

        private static readonly Color RuleColor = new Color32(81, 108, 110, 160);
        private static readonly Color KeyColor = new Color32(50, 66, 68, 243);

        private static readonly ColorBlock ButtonColors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(0.82f, 0.95f, 1f),
            pressedColor = new Color(0.6f, 0.72f, 0.78f),
            selectedColor = Color.white,
            disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };

        private static Font headingFont;
        private static Font numericFont;

        private static Font HeadingFont
        {
            get
            {
                if (headingFont == null)
                    headingFont = LoadFont(HeadingFontPath);
                return headingFont;
            }
        }

        private static Font NumericFont
        {
            get
            {
                if (numericFont == null)
                    numericFont = LoadFont(NumericFontPath);
                return numericFont;
            }
        }

        /// <summary>Creates a plate whose anchors and pivot sit at the given normalised point of the parent.</summary>
        internal static HudPlate Plate(string name, RectTransform parent, Vector2 anchor, float width, float height)
        {
            var plate = new GameObject(name, typeof(RectTransform), typeof(HudPlate)) { layer = parent.gameObject.layer };
            var rect = (RectTransform)plate.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = new Vector2(width, height);

            var graphic = plate.GetComponent<HudPlate>();
            graphic.raycastTarget = false;
            return graphic;
        }

        internal static Text Label(string name, RectTransform parent, string content, float x, float y, float width, float height,
            int size, Color tint, TextAnchor alignment = TextAnchor.MiddleLeft, bool numeric = false)
        {
            var text = Place(name, parent, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = numeric ? NumericFont : HeadingFont;
            text.fontSize = size;
            text.text = content;
            text.color = tint;
            text.alignment = alignment;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>A label centred on its parent's centre, for elements positioned by their centre, such as markers.</summary>
        internal static Text CentredLabel(string name, RectTransform parent, float width, float height, int size, Color tint, bool numeric = false)
        {
            var text = Anchored(name, parent, new Vector2(0.5f, 0.5f), width, height).gameObject.AddComponent<Text>();
            text.font = numeric ? NumericFont : HeadingFont;
            text.fontSize = size;
            text.color = tint;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Lets a label shrink down to a minimum size to fit long content.</summary>
        internal static void Fit(Text text, int minSize)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = text.fontSize;
        }

        internal static HudMeter Meter(string name, RectTransform parent, float x, float y, float width, float height)
        {
            var meter = Place(name, parent, x, y, width, height).gameObject.AddComponent<HudMeter>();
            meter.raycastTarget = false;
            return meter;
        }

        /// <summary>A one-pixel horizontal divider.</summary>
        internal static Image Rule(RectTransform parent, float x, float y, float width)
        {
            var line = Place("Rule", parent, x, y, width, 1f).gameObject.AddComponent<Image>();
            line.color = RuleColor;
            line.raycastTarget = false;
            return line;
        }

        /// <summary>A keycap showing a key name. Returns the keycap so it can be moved.</summary>
        internal static RectTransform Key(RectTransform parent, string key, float x, float y, float width = KeySize)
        {
            RectTransform keycap = Place("Keycap", parent, x, y, width, KeySize);
            var background = keycap.gameObject.AddComponent<Image>();
            background.color = KeyColor;
            background.raycastTarget = false;
            Label("Key", keycap, key, 0f, 0f, width, KeySize, 13, Ink, TextAnchor.MiddleCenter, true);
            return keycap;
        }

        /// <summary>A plate-styled button with a centred label, placed from the parent's top-left corner.</summary>
        internal static Button TextButton(string name, RectTransform parent, string label, float x, float y, float width, float height,
            int size, Color accent, Action onClick)
        {
            HudPlate plate = Plate(name, parent, new Vector2(0f, 1f), width, height);
            SetOffset(plate.rectTransform, x, y);
            plate.SetAccent(accent);
            Label("Label", plate.rectTransform, label, 0f, 0f, width, height, size, Ink, TextAnchor.MiddleCenter);
            return MakeButton(plate, onClick);
        }

        /// <summary>Turns a plate into a button. Hover and press tint the whole plate.</summary>
        internal static Button MakeButton(HudPlate plate, Action onClick)
        {
            plate.raycastTarget = true;
            var button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.colors = ButtonColors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            // A clicked button stays selected, and Enter would then submit it again. Keyboard shortcuts are handled
            // by the screens themselves, so the selection is cleared after every click.
            button.onClick.AddListener(() =>
            {
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
                onClick();
            });
            return button;
        }

        /// <summary>A full-size image covering its parent.</summary>
        internal static Image Overlay(string name, RectTransform parent, Color tint)
        {
            var image = Stretch(name, parent).gameObject.AddComponent<Image>();
            image.color = tint;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>An empty rect of a fixed size whose anchors and pivot sit at the given normalised point of the parent.</summary>
        internal static RectTransform Anchored(string name, RectTransform parent, Vector2 anchor, float width, float height)
        {
            RectTransform rect = Create(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        /// <summary>Sets up a screen-space overlay canvas scaled against 1920x1080.</summary>
        internal static void ConfigureCanvas(GameObject root, int sortingOrder)
        {
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        /// <summary>An empty rect that fills its parent.</summary>
        internal static RectTransform Stretch(string name, RectTransform parent)
        {
            RectTransform rect = Create(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>An empty rect of a fixed size, placed from the parent's top-left corner.</summary>
        internal static RectTransform Place(string name, RectTransform parent, float x, float y, float width, float height)
        {
            RectTransform rect = Create(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            SetOffset(rect, x, y);
            return rect;
        }

        /// <summary>Moves a top-left anchored rect to a position measured down from the parent's top edge.</summary>
        internal static void SetOffset(RectTransform rect, float x, float top)
        {
            rect.anchoredPosition = new Vector2(x, -top);
        }

        private static RectTransform Create(string name, RectTransform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Font LoadFont(string path)
        {
            var font = Resources.Load<Font>(path);
            return font != null ? font : Resources.GetBuiltinResource<Font>(FallbackFontName);
        }
    }
}
