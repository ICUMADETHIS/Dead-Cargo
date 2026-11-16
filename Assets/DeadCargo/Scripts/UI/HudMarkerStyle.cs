namespace DeadCargo.UI
{
    /// <summary>How one kind of HUD marker is drawn.</summary>
    internal readonly struct HudMarkerStyle
    {
        internal HudMarkerStyle(float size, bool showOffScreen)
        {
            Size = size;
            ShowOffScreen = showOffScreen;
        }

        /// <summary>Width and height of the diamond or arrow, in reference pixels.</summary>
        internal float Size { get; }

        /// <summary>When true, an off-screen target gets an arrow on the screen edge. When false, it is not shown.</summary>
        internal bool ShowOffScreen { get; }
    }
}
