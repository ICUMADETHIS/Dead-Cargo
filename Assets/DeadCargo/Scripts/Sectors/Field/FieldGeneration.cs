using Unity.Mathematics;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// Builds the rocks of one cell of an asteroid field, a pure function of the sector seed, the cell and the regions.
    /// Only asteroid-field regions hold rock; debris fields wait for wreckage of their own, and clearings thin both.
    /// Each cell tests a fixed number of candidate points and keeps each by the density there, so fields thicken and
    /// thin as their regions do. Large rocks are placed first, from their own stream, and small rocks keep out of the
    /// large rocks of their cell and its neighbours, so no boulder sits inside a giant. Sizes follow a power law: many
    /// small rocks, a few large ones, a rare giant. Plain value code over <see cref="IRegionSet"/> and
    /// <see cref="IRockBuffer"/>, so it runs in Burst jobs and in the self-test alike.
    /// </summary>
    public static class FieldGeneration
    {
        // Separate streams, so large and small rocks never shift each other, nor any other content of a cell.
        private const uint LargeStream = 0xF1E1D001u;
        private const uint SmallStream = 0xF1E1D002u;

        private const uint ClumpStream = 0xF1E1D003u;
        private const uint RubbleStream = 0xF1E1D004u;

        // Never search further than this many cells out for large rocks, whatever the settings.
        private const int MaxKeepOutRing = 2;

        // Clumps: broad shapes with a finer layer breaking up their edges, cut so about half of space is clump.
        private const float ClumpDetailScale = 2.6f;
        private const float ClumpDetailShare = 0.35f;
        private const float ClumpEdge = 0.3f;

        // Rubble: a large rock of this radius or more gathers its full share, out to this many radii past its keep-out.
        private const float FullRubbleRadius = 200f;
        private const float RubbleReach = 1.5f;

        /// <summary>Where in the noise a sector's clumps are taken from, so each seed clumps its own way.</summary>
        public static float3 ClumpOffset(uint sectorSeed)
        {
            var random = new SectorRandom(SectorHash.Combine(sectorSeed, ClumpStream));
            return new float3(random.NextFloat(), random.NextFloat(), random.NextFloat()) * 1000f;
        }

        /// <summary>
        /// The share of a field's density kept at a position, from 1 inside a clump down towards 0 in the gaps between.
        /// A pure function of the position, so cells agree across their borders.
        /// </summary>
        public static float Clump(in FieldSettings settings, float3 clumpOffset, float3 position)
        {
            if (settings.Clumping <= 0f)
                return 1f;

            float3 point = position / settings.ClumpSize + clumpOffset;
            float broad = noise.snoise(point);
            float detail = noise.snoise(point * ClumpDetailScale + 17.3f);
            float shape = math.smoothstep(-ClumpEdge, ClumpEdge, math.lerp(broad, detail, ClumpDetailShare));
            return math.lerp(1f, shape, settings.Clumping);
        }

        /// <summary>How many cells out the large rocks reach that small rocks of a cell must keep out of.</summary>
        public static int KeepOutRing(in FieldSettings settings, float cellSize)
        {
            float reach = settings.MaxDiameter * 0.5f * (1f + settings.Stretch) * settings.KeepOut;
            return math.clamp((int)math.ceil(reach / math.max(cellSize, 1f)), 1, MaxKeepOutRing);
        }

        /// <summary>Room a cell needs for the large rocks around it, which <see cref="Cell"/> collects first.</summary>
        public static int NearbyCapacity(in FieldSettings settings, float cellSize)
        {
            int across = KeepOutRing(settings, cellSize) * 2 + 1;
            return across * across * across * settings.LargeCandidates;
        }

        /// <summary>Whether a cell can hold any rock: false when no asteroid field reaches it, which skips it entirely.</summary>
        public static bool MayHoldRocks<TRegions>(in TRegions regions, in SectorGrid grid, CellCoord cell)
            where TRegions : struct, IRegionSet
        {
            if (!grid.Contains(cell))
                return false;

            float3 min = grid.CellMin(cell);
            return SectorDensity.AnyOverlaps(regions, SectorRegionKind.AsteroidField, min, min + grid.CellSize);
        }

        /// <summary>
        /// Writes a cell's rocks, large ones first. <paramref name="nearby"/> is scratch for the large rocks around the
        /// cell; it needs <see cref="NearbyCapacity"/> room and starts empty.
        /// </summary>
        public static void Cell<TRegions, TRocks, TNearby>(in FieldSettings settings, in TRegions regions, in SectorGrid grid,
            uint sectorSeed, CellCoord cell, ref TRocks rocks, ref TNearby nearby)
            where TRegions : struct, IRegionSet
            where TRocks : struct, IRockBuffer
            where TNearby : struct, IRockBuffer
        {
            if (!MayHoldRocks(regions, grid, cell))
                return;

            int ring = KeepOutRing(settings, grid.CellSize);
            for (int z = -ring; z <= ring; ++z)
            {
                for (int y = -ring; y <= ring; ++y)
                {
                    for (int x = -ring; x <= ring; ++x)
                        LargeRocks(settings, regions, grid, sectorSeed, new CellCoord(cell.X + x, cell.Y + y, cell.Z + z), ref nearby);
                }
            }

            LargeRocks(settings, regions, grid, sectorSeed, cell, ref rocks);
            SmallRocks(settings, regions, grid, sectorSeed, cell, nearby, ref rocks);
        }

        /// <summary>A cell's large rocks: those from <see cref="FieldSettings.LargeDiameter"/> up to the largest.</summary>
        public static void LargeRocks<TRegions, TRocks>(in FieldSettings settings, in TRegions regions, in SectorGrid grid,
            uint sectorSeed, CellCoord cell, ref TRocks rocks)
            where TRegions : struct, IRegionSet
            where TRocks : struct, IRockBuffer
        {
            if (!MayHoldRocks(regions, grid, cell))
                return;

            float3 min = grid.CellMin(cell);
            float size = grid.CellSize;
            float3 clumpOffset = ClumpOffset(sectorSeed);
            var random = new SectorRandom(SectorHash.Cell(sectorSeed, cell, LargeStream));

            for (int i = 0; i < settings.LargeCandidates; ++i)
            {
                // Every candidate draws the same values, kept or not, so one never shifts the next.
                float3 position = min + new float3(random.NextFloat(), random.NextFloat(), random.NextFloat()) * size;
                float keep = random.NextFloat();
                float sizeRoll = random.NextFloat();
                uint seed = random.NextUInt();

                float density = SectorDensity.DensityOf(regions, SectorRegionKind.AsteroidField, position) * Clump(settings, clumpOffset, position);
                if (keep >= density * settings.LargeChance)
                    continue;

                float diameter = Diameter(sizeRoll, settings.LargeDiameter, settings.MaxDiameter, settings.SizeExponent);
                if (Sheltered(regions, position, Reach(settings, diameter * 0.5f)))
                    continue;

                rocks.Add(new FieldRock(position, diameter * 0.5f, seed));
            }
        }

        private static void SmallRocks<TRegions, TNearby, TRocks>(in FieldSettings settings, in TRegions regions, in SectorGrid grid,
            uint sectorSeed, CellCoord cell, in TNearby nearby, ref TRocks rocks)
            where TRegions : struct, IRegionSet
            where TNearby : struct, IRockBuffer
            where TRocks : struct, IRockBuffer
        {
            float3 min = grid.CellMin(cell);
            float size = grid.CellSize;
            float keepOut = settings.KeepOut * (1f + settings.Stretch);
            int large = nearby.Count;

            // The cell's own large rocks were written first; rubble gathers round them.
            int own = rocks.Count;
            float3 clumpOffset = ClumpOffset(sectorSeed);
            var random = new SectorRandom(SectorHash.Cell(sectorSeed, cell, SmallStream));

            for (int i = 0; i < settings.SmallCandidates; ++i)
            {
                float3 position = min + new float3(random.NextFloat(), random.NextFloat(), random.NextFloat()) * size;
                float keep = random.NextFloat();
                float sizeRoll = random.NextFloat();
                uint seed = random.NextUInt();

                // Rubble sits where its large rock is, clump or gap; every other rock follows the clumps.
                float radius = Diameter(sizeRoll, settings.MinDiameter, settings.LargeDiameter, settings.SizeExponent) * 0.5f;
                bool gathered = own > 0 && settings.Rubble > 0f &&
                                Gather(settings, rocks, own, min, size, radius, seed, keepOut, ref position);
                float density = SectorDensity.DensityOf(regions, SectorRegionKind.AsteroidField, position);
                if (!gathered)
                    density *= Clump(settings, clumpOffset, position);

                if (keep >= density * settings.SmallChance)
                    continue;

                if (InsideLarge(nearby, large, position, radius, keepOut) || Sheltered(regions, position, Reach(settings, radius)))
                    continue;

                rocks.Add(new FieldRock(position, radius, seed));
            }
        }

        /// <summary>The furthest a rock's surface can reach from its centre: its radius at the most stretch and the largest lump.</summary>
        public static float Reach(in FieldSettings settings, float radius)
        {
            return radius * (1f + settings.Stretch) * FieldRockShape.MaxRadius;
        }

        // Placed content shelters its spot: a rock is dropped when its surface could reach into a shelter. A pure test of
        // the rock's own position and size, so neighbouring cells agree.
        private static bool Sheltered<TRegions>(in TRegions regions, float3 position, float reach)
            where TRegions : struct, IRegionSet
        {
            int count = regions.Count;
            for (int i = 0; i < count; ++i)
            {
                RegionVolume region = regions.Get(i);
                if (region.Shelter <= 0f)
                    continue;

                float limit = region.Shelter + reach;
                if (math.distancesq(position, region.Centre) < limit * limit)
                    return true;
            }

            return false;
        }

        // Moves a small rock to just outside one of its cell's large rocks, nearer more often than further, when its
        // seed says so. Giants gather the most. A place outside the cell is refused, so a cell never writes rock into
        // its neighbour. Drawn from the rock's own seed, so the cell's stream is left as it was.
        private static bool Gather<TRocks>(in FieldSettings settings, in TRocks rocks, int own, float3 min, float size, float radius,
            uint seed, float keepOut, ref float3 position)
            where TRocks : struct, IRockBuffer
        {
            var random = new SectorRandom(SectorHash.Combine(seed, RubbleStream));
            if (random.NextFloat() >= settings.Rubble)
                return false;

            FieldRock large = rocks.Get(random.Range(0, own));
            if (random.NextFloat() >= math.saturate(large.Radius / FullRubbleRadius))
                return false;

            float height = random.NextFloat() * 2f - 1f;
            float angle = random.NextFloat() * (math.PI * 2f);
            float across = math.sqrt(math.max(0f, 1f - height * height));
            var direction = new float3(across * math.cos(angle), across * math.sin(angle), height);

            float spread = random.NextFloat();
            float distance = large.Radius * (keepOut + RubbleReach * spread * spread) + radius;
            float3 place = large.Position + direction * distance;
            if (math.any(place < min) || math.any(place >= min + size))
                return false;

            position = place;
            return true;
        }

        private static bool InsideLarge<TNearby>(in TNearby nearby, int count, float3 position, float radius, float keepOut)
            where TNearby : struct, IRockBuffer
        {
            for (int i = 0; i < count; ++i)
            {
                FieldRock rock = nearby.Get(i);
                float reach = rock.Radius * keepOut + radius;
                if (math.distancesq(position, rock.Position) < reach * reach)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A diameter from a roll of 0 to 1, spread so that the count of rocks larger than a size falls as that size to
        /// the power <paramref name="exponent"/>: the inverse of that spread's cumulative share.
        /// </summary>
        public static float Diameter(float roll, float min, float max, float exponent)
        {
            float smallest = math.pow(min, -exponent);
            float largest = math.pow(max, -exponent);
            return math.clamp(math.pow(smallest - roll * (smallest - largest), -1f / exponent), min, max);
        }
    }
}
