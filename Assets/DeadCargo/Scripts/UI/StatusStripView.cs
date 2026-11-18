using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.UI
{
    /// <summary>
    /// Builds and updates the status strip: one leaning, ink-outlined strip at the bottom centre holding the ship's shield
    /// and hull, its speed and throttle, and both weapons with their ammunition. Text is only rebuilt when its whole figure
    /// changes; bars move every frame. Positions are in reference pixels from the strip's top-left corner.
    /// </summary>
    internal sealed class StatusStripView
    {
        internal const int WeaponSlots = 2;

        private const float Width = 930f;
        private const float Height = 81f;
        private const float BottomGap = 26f;
        private const float ShadowDrop = 8f;

        /// <summary>How much of the screen's bottom the strip covers, shadow included, in reference pixels.</summary>
        internal const float CoveredHeight = BottomGap + Height + ShadowDrop;
        private const float DividerWidth = 5f;
        private const int MaxPips = 8;

        // Below this share the figure turns to the warning colour.
        private const float LowShare = 0.3f;

        internal static readonly Color Accent = new Color32(242, 163, 58, 255);
        private static readonly Color Face = new Color32(24, 27, 34, 245);
        private static readonly Color Track = new Color32(10, 11, 14, 255);
        private static readonly Color ShieldColor = HudFactory.CruiseColor;
        private static readonly Color HullColor = HudFactory.CargoColor;
        private static readonly Color BoostColor = HudFactory.CruiseColor;
        private static readonly Color SpentPip = new Color32(58, 62, 71, 255);
        internal static readonly Color Figure = new Color32(243, 238, 228, 255);

        private readonly RectTransform root;
        private readonly HudSticker shieldBar;
        private readonly HudSticker hullBar;
        private readonly Text shieldText;
        private readonly Text hullText;
        private readonly Text speedText;
        private readonly Text unitText;
        private readonly HudSticker throttleBar;
        private readonly Weapon[] weapons = new Weapon[WeaponSlots];

        private int shownShield = -1;
        private int shownHull = -1;
        private int shownSpeed = -1;
        private bool shownCruise;
        private bool shownBoost;

        internal StatusStripView(RectTransform parent)
        {
            root = HudFactory.Anchored("Status Strip", parent, new Vector2(0.5f, 0f), Width, Height);
            root.anchoredPosition = new Vector2(0f, BottomGap);

            HudSticker body = Sticker("Body", root, 0f, 0f, Width, Height, Face, 4f, new Vector2(ShadowDrop, -ShadowDrop));
            RectTransform face = body.rectTransform;
            Divider(face, 280f);
            Divider(face, 530f);
            Divider(face, 730f);

            shieldBar = Sticker("Shield", face, 30f, 16f, 182f, 16f, ShieldColor, 3f, Vector2.zero);
            shieldBar.SetTrack(Track);
            shieldText = Figures("Shield Figure", face, 218f, 6f, 56f, 34f, 28, ShieldColor);
            hullBar = Sticker("Hull", face, 22f, 46f, 182f, 16f, HullColor, 3f, Vector2.zero);
            hullBar.SetTrack(Track);
            hullText = Figures("Hull Figure", face, 210f, 36f, 56f, 34f, 28, HullColor);

            speedText = Figures("Speed", face, 300f, 4f, 132f, 72f, 60, Figure);
            unitText = Caption("Speed Unit", face, "M/S", 438f, 14f, 80f, 20f, 15, HudFactory.Muted);
            throttleBar = Sticker("Throttle", face, 432f, 44f, 80f, 14f, Accent, 3f, Vector2.zero);
            throttleBar.SetTrack(Track);

            weapons[0] = new Weapon(face, 552f);
            weapons[1] = new Weapon(face, 752f);
        }

        internal bool Visible => root.gameObject.activeSelf;

        internal void SetVisible(bool visible)
        {
            if (root.gameObject.activeSelf != visible)
                root.gameObject.SetActive(visible);
        }

        /// <summary>Shield and hull as shares of full, or a negative share to leave that row empty.</summary>
        internal void SetHealth(float shield, float hull)
        {
            SetRow(shieldBar, shieldText, shield, ShieldColor, ref shownShield);
            SetRow(hullBar, hullText, hull, HullColor, ref shownHull);
        }

        /// <summary>Speed in metres a second, or in cruise, kilometres a second; throttle 0 to 1, and boost.</summary>
        internal void SetSpeed(double speed, bool cruising, float throttle, bool boosting)
        {
            int figure = (int)System.Math.Round(cruising ? speed / 1000.0 : speed);
            if (figure != shownSpeed)
            {
                shownSpeed = figure;
                speedText.text = figure.ToString("N0");
            }

            if (cruising != shownCruise)
            {
                shownCruise = cruising;
                unitText.text = cruising ? "KM/S" : "M/S";
                unitText.color = cruising ? HudFactory.CruiseColor : HudFactory.Muted;
            }

            if (boosting != shownBoost)
            {
                shownBoost = boosting;
                throttleBar.SetFace(boosting ? BoostColor : Accent);
            }

            throttleBar.SetFill(boosting ? 1f : throttle);
        }

        /// <summary>One weapon slot: its name, its rounds and how many it holds, or none for a weapon that never runs dry.</summary>
        internal void SetWeapon(int slot, bool present, string label, int rounds, int capacity)
        {
            weapons[slot].Set(present, slot + 1, label, rounds, capacity);
        }

        private static void SetRow(HudSticker bar, Text text, float share, Color tint, ref int shown)
        {
            bool present = share >= 0f;
            if (bar.gameObject.activeSelf != present)
            {
                bar.gameObject.SetActive(present);
                text.gameObject.SetActive(present);
            }

            if (!present)
                return;

            bar.SetFill(share);
            int figure = Mathf.RoundToInt(share * 100f);
            if (figure == shown)
                return;

            shown = figure;
            text.text = figure.ToString();
            text.color = share < LowShare ? HudFactory.WarningColor : tint;
        }

        private static HudSticker Sticker(string name, RectTransform parent, float x, float y, float width, float height, Color face,
            float outline, Vector2 shadow)
        {
            RectTransform rect = HudFactory.Place(name, parent, x, y, width, height);
            var sticker = rect.gameObject.AddComponent<HudSticker>();
            sticker.Configure(face, outline, shadow, HudSticker.DefaultSlant);
            return sticker;
        }

        // An ink band across the strip, leaning with it, between two of its sections.
        private static void Divider(RectTransform parent, float x)
        {
            Sticker("Divider", parent, x, 0f, DividerWidth + Height * HudSticker.DefaultSlant, Height, HudSticker.Ink, 0f, Vector2.zero);
        }

        // Big leaning figures: the strip's speed, health and rounds. They never wrap, so a wide figure still shows whole.
        private static Text Figures(string name, RectTransform parent, float x, float y, float width, float height, int size, Color tint)
        {
            Text text = HudFactory.Label(name, parent, "", x, y, width, height, size, tint);
            text.font = HudFactory.FigureFont;
            text.fontStyle = FontStyle.Italic;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // Small caps labels: units, weapon names, capacities.
        private static Text Caption(string name, RectTransform parent, string content, float x, float y, float width, float height, int size,
            Color tint)
        {
            Text text = HudFactory.Label(name, parent, content, x, y, width, height, size, tint);
            text.font = HudFactory.StickerFont;
            HudFactory.Unclipped(text);
            return text;
        }

        // One weapon's section: rounds left in big figures, its key and name, and what it holds as pips or a figure.
        private sealed class Weapon
        {
            private readonly RectTransform section;
            private readonly Text rounds;
            private readonly Text label;
            private readonly Text capacityText;
            private readonly HudSticker[] pips = new HudSticker[MaxPips];

            private int shownRounds = int.MinValue;
            private int shownCapacity = int.MinValue;
            private string shownLabel;

            // The name runs across the top of the section; the rounds sit under it, with what it holds beside them.
            internal Weapon(RectTransform parent, float x)
            {
                section = HudFactory.Place("Weapon", parent, x, 0f, 176f, Height);
                label = Caption("Name", section, "", 14f, 5f, 160f, 18f, 13, Accent);
                rounds = Figures("Rounds", section, 4f, 22f, 64f, 54f, 44, Figure);
                rounds.alignment = TextAnchor.MiddleRight;
                capacityText = Caption("Capacity", section, "", 72f, 44f, 90f, 22f, 17, HudFactory.Muted);
                capacityText.font = HudFactory.FigureFont;
                for (int i = 0; i < MaxPips; ++i)
                {
                    pips[i] = Sticker("Pip", section, 72f + i * 12f, 46f, 10f, 15f, Figure, 2f, Vector2.zero);
                    pips[i].gameObject.SetActive(false);
                }
            }

            internal void Set(bool present, int key, string name, int count, int capacity)
            {
                if (section.gameObject.activeSelf != present)
                    section.gameObject.SetActive(present);

                if (!present)
                    return;

                if (name != shownLabel)
                {
                    shownLabel = name;
                    label.text = $"{key}  {name}";
                    HudFactory.Fit(label, 11);
                }

                if (count == shownRounds && capacity == shownCapacity)
                    return;

                shownRounds = count;
                shownCapacity = capacity;
                bool counted = capacity > 0;
                rounds.text = counted ? count.ToString() : "--";
                rounds.color = counted && count == 0 ? HudFactory.WarningColor : Figure;

                bool usePips = counted && capacity <= MaxPips;
                capacityText.gameObject.SetActive(counted && !usePips);
                if (counted && !usePips)
                    capacityText.text = $"/{capacity}";

                for (int i = 0; i < MaxPips; ++i)
                {
                    bool shown = usePips && i < capacity;
                    if (pips[i].gameObject.activeSelf != shown)
                        pips[i].gameObject.SetActive(shown);
                    if (shown)
                        pips[i].SetFace(i < count ? Figure : SpentPip);
                }
            }
        }
    }
}
