namespace DeadCargo.Cargo
{
    /// <summary>Where an item is stored in a ship's hold.</summary>
    public enum CargoSlot
    {
        /// <summary>The main hold, limited by mass. Can be jettisoned.</summary>
        Hold,

        /// <summary>The sealed dark matter slot, limited by units. Weightless and cannot be jettisoned.</summary>
        DarkMatter
    }
}
