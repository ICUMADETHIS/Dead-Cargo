using System.Collections.Generic;

namespace DeadCargo.Sectors
{
    /// <summary>A run's regions as an <see cref="IRegionSet"/>, read in place with no copy.</summary>
    public readonly struct LayoutRegionSet : IRegionSet
    {
        private readonly List<RegionVolume> regions;

        public LayoutRegionSet(List<RegionVolume> regions)
        {
            this.regions = regions;
        }

        public int Count => regions != null ? regions.Count : 0;

        public RegionVolume Get(int index)
        {
            return regions[index];
        }
    }
}
