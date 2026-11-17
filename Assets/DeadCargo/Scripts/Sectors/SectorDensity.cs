using Unity.Mathematics;

namespace DeadCargo.Sectors
{
    /// <summary>
    /// How full space is at a position: the one implementation behind <see cref="SectorRegion.StrengthAt"/>,
    /// <see cref="SectorDefinition.DensityAt"/> and every generation job. Plain value maths, so it runs in Burst.
    /// </summary>
    public static class SectorDensity
    {
        /// <summary>Returned for a position in no field.</summary>
        public const int NoRegion = -1;

        /// <summary>A region's strength at a sector-space position, from 0 outside it to its density at the centre.</summary>
        public static float Strength(in RegionVolume region, float3 position)
        {
            if (!math.all(region.Radii > 0f) || math.any(math.abs(position - region.Centre) > region.Extent))
                return 0f;

            // Distance from the centre as a share of the radius: 0 at the centre, 1 on the surface.
            float3 offset = region.ToLocal(position) / region.Radii;
            float distanceSquared = math.dot(offset, offset);
            if (distanceSquared >= 1f)
                return 0f;

            if (region.Falloff <= 0f)
                return region.Density;

            float fade = (1f - math.sqrt(distanceSquared)) / region.Falloff;
            if (fade >= 1f)
                return region.Density;

            // Smoothstep, so the edge has no visible ring.
            return region.Density * fade * fade * (3f - 2f * fade);
        }

        /// <summary>
        /// Density at a sector-space position, from 0 to 1: the strongest field there, thinned by the strongest
        /// clearing. <paramref name="regionIndex"/> is the field that decides the content, or <see cref="NoRegion"/>.
        /// </summary>
        public static float DensityAt<TRegions>(in TRegions regions, float3 position, out int regionIndex)
            where TRegions : struct, IRegionSet
        {
            regionIndex = NoRegion;
            float density = 0f;
            float cleared = 0f;
            int count = regions.Count;

            for (int i = 0; i < count; ++i)
            {
                RegionVolume region = regions.Get(i);
                float strength = Strength(region, position);
                if (strength <= 0f)
                    continue;

                if (!region.IsField)
                {
                    cleared = math.max(cleared, strength);
                }
                else if (strength > density)
                {
                    density = strength;
                    regionIndex = i;
                }
            }

            density *= 1f - cleared;
            if (density <= 0f)
                regionIndex = NoRegion;

            return density;
        }

        /// <summary>
        /// Density of one kind of content at a position: the density there when a field of that kind decides it, else 0.
        /// </summary>
        public static float DensityOf<TRegions>(in TRegions regions, SectorRegionKind kind, float3 position)
            where TRegions : struct, IRegionSet
        {
            float density = DensityAt(regions, position, out int regionIndex);
            return regionIndex != NoRegion && regions.Get(regionIndex).Kind == kind ? density : 0f;
        }

        /// <summary>
        /// Whether a field of one kind reaches into a box, judged by bounding boxes. False means the box holds none of
        /// that content.
        /// </summary>
        public static bool AnyOverlaps<TRegions>(in TRegions regions, SectorRegionKind kind, float3 boxMin, float3 boxMax)
            where TRegions : struct, IRegionSet
        {
            int count = regions.Count;
            for (int i = 0; i < count; ++i)
            {
                RegionVolume region = regions.Get(i);
                if (region.Kind == kind && region.MayOverlap(boxMin, boxMax))
                    return true;
            }

            return false;
        }
    }
}
