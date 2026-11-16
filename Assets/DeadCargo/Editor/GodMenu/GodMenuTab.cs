namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>
    /// One collapsible tab of the God Menu.
    /// To add a tab, create a class that derives from this one. The window finds it automatically;
    /// no other file needs to change. Group controls with <see cref="GodMenuGUI.SectionScope"/>.
    /// </summary>
    internal abstract class GodMenuTab
    {
        /// <summary>Name shown on the tab's header bar.</summary>
        public abstract string Title { get; }

        /// <summary>The headed group the tab sits in.</summary>
        public abstract GodMenuGroup Group { get; }

        /// <summary>Position within the group. Lower values appear first. Leave gaps of ten so tabs can be inserted later.</summary>
        public abstract int Order { get; }

        /// <summary>
        /// Whether the tab applies to the current mode and scene. Hidden tabs are not drawn at all,
        /// so a tab never shows controls that cannot be used. Read after <see cref="OnUpdate"/>, outside OnGUI.
        /// </summary>
        public virtual bool IsAvailable => true;

        /// <summary>A few words of live status shown on the header, so the tab is useful while collapsed. Null for none.</summary>
        public virtual string Summary => null;

        /// <summary>Called about ten times a second while the window is open, whether or not the tab is shown.</summary>
        public virtual void OnUpdate() { }

        /// <summary>Draws the tab's contents. Only called while the tab is available and expanded.</summary>
        public abstract void OnGUI();
    }
}
