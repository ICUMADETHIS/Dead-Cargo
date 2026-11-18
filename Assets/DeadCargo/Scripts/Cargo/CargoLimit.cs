namespace DeadCargo.Cargo
{
    /// <summary>What stopped cargo from fitting aboard.</summary>
    public enum CargoLimit
    {
        /// <summary>The hold's mass capacity, in tonnes.</summary>
        Mass,

        /// <summary>The hold grid has no free space the item fits.</summary>
        Space,

        /// <summary>The sealed dark matter slot is full.</summary>
        DarkMatter
    }
}
