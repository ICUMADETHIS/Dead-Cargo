using Unity.Collections;

namespace DeadCargo.Sectors.Field
{
    /// <summary>A sector's regions in native memory, as generation jobs read them.</summary>
    public readonly struct NativeRegionSet : IRegionSet
    {
        private readonly NativeArray<RegionVolume> regions;

        public NativeRegionSet(NativeArray<RegionVolume> regions)
        {
            this.regions = regions;
        }

        public int Count => regions.Length;

        public RegionVolume Get(int index)
        {
            return regions[index];
        }
    }
}
