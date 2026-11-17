namespace DeadCargo.Sectors
{
    /// <summary>
    /// A sector's regions, read one at a time. Implemented over a definition's array in the editor and over a native
    /// array in generation jobs, so <see cref="SectorDensity"/> is one implementation for both.
    /// </summary>
    public interface IRegionSet
    {
        int Count { get; }

        RegionVolume Get(int index);
    }
}
