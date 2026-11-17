using System.Collections.Generic;

namespace DeadCargo.Sectors
{
    /// <summary>A definition's regions as an <see cref="IRegionSet"/>, read in place with no copy.</summary>
    public readonly struct SectorRegionSet : IRegionSet
    {
        private readonly IReadOnlyList<SectorRegion> regions;

        public SectorRegionSet(IReadOnlyList<SectorRegion> regions)
        {
            this.regions = regions;
        }

        public int Count => regions != null ? regions.Count : 0;

        public RegionVolume Get(int index)
        {
            return regions[index].Volume;
        }
    }
}
