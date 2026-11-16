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

        /// <summary>Position in the window. Lower values appear first. Leave gaps so tabs can be inserted later.</summary>
        public abstract int Order { get; }

        /// <summary>Called about ten times a second while the window is open, whether or not the tab is expanded.</summary>
        public virtual void OnUpdate() { }

        /// <summary>Draws the tab's contents. Only called while the tab is expanded.</summary>
        public abstract void OnGUI();
    }
}
