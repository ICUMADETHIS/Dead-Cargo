namespace DeadCargo.Flight
{
    /// <summary>Why cruise ended.</summary>
    public enum CruiseExit
    {
        /// <summary>The player dropped out.</summary>
        Player,

        /// <summary>The ship reached a planet's drop height, above its ground.</summary>
        Planet,

        /// <summary>The ship came back within reach of the sector.</summary>
        Home,

        /// <summary>The ship hit something.</summary>
        Collision,

        /// <summary>The drive was switched off, or the ship lost.</summary>
        Interrupted
    }
}
