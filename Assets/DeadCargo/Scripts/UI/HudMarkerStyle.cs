namespace DeadCargo.UI
{
    /// <summary>How one kind of HUD marker is drawn.</summary>
    internal readonly struct HudMarkerStyle
    {
        internal HudMarkerStyle(float size, bool showOffScreen, bool inKilometres = false)
        {
            Size = size;
            ShowOffScreen = showOffScreen;
            InKilometres = inKilometres;
        }

        /// <summary>Width and height of the diamond or arrow, in reference pixels.</summary>
        internal float Size { get; }

        /// <summary>When true, an off-screen target gets an arrow on the screen edge. When false, it is not shown.</summary>
        internal bool ShowOffScreen { get; }

        /// <summary>When true, the label's figure is in kilometres rather than metres, for planets and far places.</summary>
        internal bool InKilometres { get; }
    }
}
