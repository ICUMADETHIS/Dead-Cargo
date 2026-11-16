namespace DeadCargo.Sectors
{
    /// <summary>What a region of a sector is filled with.</summary>
    public enum SectorRegionKind
    {
        /// <summary>Rock. The bulk of a sector's content.</summary>
        AsteroidField,

        /// <summary>Wreckage and scrap.</summary>
        DebrisField,

        /// <summary>Open space. Thins out every field it overlaps, for spawn areas and flight lanes.</summary>
        Clearing
    }
}
