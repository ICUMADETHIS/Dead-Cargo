using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace DeadCargo.Sectors
{
    /// <summary>
    /// The regions of one run: the definition's regions, each varied within its limits by the run seed and kept inside
    /// the sector, followed by the clearings that placed content opens round itself. A pure function of the definition
    /// and the seed, rebuilt whenever the seed changes, so every system that reads it agrees and nothing is saved.
    /// The arrival point, where the player starts, is the sector's origin; regions swing round it.
    /// </summary>
    public sealed class SectorLayout
    {
        // Separates region variation from every other use of the sector seed.
        private const uint VariationStream = 0x1A7007A1u;

        // Clearings round placed content fade over half their radius, so the rock thins rather than stopping at a wall.
        private const float ClearingFalloff = 0.5f;

        private readonly List<RegionVolume> regions = new List<RegionVolume>();
        private readonly List<string> names = new List<string>();

        /// <summary>Every region of the run: the varied authored regions first, then the clearings added since.</summary>
        public int Count => regions.Count;

        /// <summary>How many of the regions come from the definition. The rest are clearings added by placed content.</summary>
        public int AuthoredCount { get; private set; }

        public RegionVolume this[int index] => regions[index];

        /// <summary>The regions as an <see cref="IRegionSet"/>, for <see cref="SectorDensity"/>.</summary>
        public LayoutRegionSet Set => new LayoutRegionSet(regions);

        public string NameOf(int index)
        {
            return index >= 0 && index < names.Count ? names[index] : string.Empty;
        }

        /// <summary>Replaces the layout with the definition's regions, varied by the seed when <paramref name="vary"/> is set.</summary>
        public void Build(IReadOnlyList<SectorRegion> authored, in SectorGrid grid, uint seed, bool vary)
        {
            regions.Clear();
            names.Clear();

            int count = authored != null ? authored.Count : 0;
            for (int i = 0; i < count; ++i)
            {
                SectorRegion region = authored[i];
                regions.Add(vary ? Vary(region, grid, RegionSeed(seed, i)) : region.Volume);
                names.Add(string.IsNullOrEmpty(region.Name) ? $"Region {i}" : region.Name);
            }

            AuthoredCount = count;
        }

        /// <summary>
        /// Opens a round clearing for placed content: it thins every field it overlaps, and no rock reaches into it,
        /// so even a giant whose centre lies outside keeps its body clear.
        /// </summary>
        public void AddClearing(string name, Vector3 centre, float radius)
        {
            if (radius <= 0f)
                return;

            RegionVolume clearing = RegionVolume.Create(centre, new float3(radius), 0f, 1f, ClearingFalloff, SectorRegionKind.Clearing);
            clearing.Shelter = radius;
            regions.Add(clearing);
            names.Add(name);
        }

        /// <summary>
        /// How full space is at a sector-space position, from 0 to 1, and the index of the region that decides the
        /// content there, or <see cref="SectorDensity.NoRegion"/>.
        /// </summary>
        public float DensityAt(Vector3 sectorPosition, out int regionIndex)
        {
            return SectorDensity.DensityAt(Set, sectorPosition, out regionIndex);
        }

        /// <summary>The seed one authored region draws its variation from.</summary>
        public static uint RegionSeed(uint sectorSeed, int regionIndex)
        {
            return SectorHash.Combine(SectorHash.Combine(sectorSeed, VariationStream), unchecked((uint)regionIndex));
        }

        /// <summary>
        /// One region as a run sees it: swung round the arrival point, moved, turned and resized within its limits,
        /// then moved back inside the sector if it reaches outside. A fixed region is returned exactly as authored.
        /// </summary>
        public static RegionVolume Vary(in SectorRegion region, in SectorGrid grid, uint regionSeed)
        {
            RegionVariation limits = region.Variation;
            if (limits.IsFixed)
                return region.Volume;

            // Every value is drawn whatever the limits, so one limit never shifts another's draw.
            var random = new SectorRandom(regionSeed);
            float orbit = random.Range(-1f, 1f) * limits.Orbit;
            Vector3 drift = Vector3.Scale(new Vector3(random.Range(-1f, 1f), random.Range(-1f, 1f), random.Range(-1f, 1f)), limits.Drift);
            float turn = random.Range(-1f, 1f) * limits.Turn;
            var scale = new Vector3(
                1f + random.Range(-1f, 1f) * limits.Resize,
                1f + random.Range(-1f, 1f) * limits.Resize,
                1f + random.Range(-1f, 1f) * limits.Resize);

            // The swing turns the centre about the vertical axis through the arrival point, the way a yaw turns a direction.
            math.sincos(math.radians(orbit), out float sin, out float cos);
            Vector3 authoredCentre = region.Centre;
            Vector3 centre = new Vector3(
                authoredCentre.x * cos + authoredCentre.z * sin,
                authoredCentre.y,
                authoredCentre.z * cos - authoredCentre.x * sin) + drift;
            Vector3 radii = Vector3.Scale(region.Radii, scale);
            float yaw = Mathf.DeltaAngle(0f, region.Yaw + orbit + turn);
            RegionVolume volume = RegionVolume.Create(centre, radii, yaw, region.Density, region.Falloff, region.Kind);

            // Content is only generated inside the grid. A region too wide for an axis is centred on it instead.
            float3 gridMin = grid.Min;
            float3 gridMax = gridMin + (float3)grid.Size;
            float3 low = gridMin + volume.Extent;
            float3 high = gridMax - volume.Extent;
            volume.Centre = math.select(math.clamp(volume.Centre, low, high), (gridMin + gridMax) * 0.5f, low > high);
            return volume;
        }
    }
}
