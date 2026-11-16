namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>The headed groups the God Menu sorts its tabs into, in display order.</summary>
    internal enum GodMenuGroup
    {
        /// <summary>Play controls and the game loop. Always shown.</summary>
        Game,

        /// <summary>Live tools for a running sector. Shown in Play Mode when their system is in the scene.</summary>
        Run,

        /// <summary>Scene setup and project tools. Shown outside Play Mode.</summary>
        Editor
    }
}
