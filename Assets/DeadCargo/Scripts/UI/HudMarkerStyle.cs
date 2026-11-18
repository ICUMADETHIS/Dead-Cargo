namespace DeadCargo.UI
{
    /// <summary>How a marker's label reads.</summary>
    internal enum HudMarkerLook
    {
        /// <summary>The glyph alone, for small things such as pickups.</summary>
        Plain,

        /// <summary>A charcoal sticker with the name in the marker's colour, for places that are only nearby.</summary>
        Quiet,

        /// <summary>A sticker in the marker's colour with ink text, for what the player is heading for or running from.</summary>
        Loud,
    }

    /// <summary>How one kind of HUD marker is drawn.</summary>
    internal readonly struct HudMarkerStyle
    {
        internal HudMarkerStyle(float size, bool showOffScreen, HudMarkerLook look, bool inKilometres = false)
        {
            Size = size;
            ShowOffScreen = showOffScreen;
            Look = look;
            InKilometres = inKilometres;
        }

        /// <summary>Width and height of the diamond, or length of the arrow, in reference pixels, without its ink.</summary>
        internal float Size { get; }

        /// <summary>When true, an off-screen target gets an arrow on the screen edge. When false, it is not shown.</summary>
        internal bool ShowOffScreen { get; }

        /// <summary>How the label reads, and how bold the glyph's ink is.</summary>
        internal HudMarkerLook Look { get; }

        /// <summary>When true, the label's figure is in kilometres rather than metres, for planets and far places.</summary>
        internal bool InKilometres { get; }

        /// <summary>The glyph's ink outline, in reference pixels.</summary>
        internal float Outline => Look == HudMarkerLook.Loud ? 3f : Look == HudMarkerLook.Quiet ? 2.5f : 2f;
    }
}
