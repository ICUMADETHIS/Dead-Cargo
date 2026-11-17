using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using DeadCargo.Flight;
using DeadCargo.Sectors;
using DeadCargo.Sectors.Far;
using DeadCargo.Sectors.Field;
using DeadCargo.Sectors.Placement;
using DeadCargo.Visuals;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DeadCargo.EditorTools.Diagnostics
{
    /// <summary>
    /// Checks the maths that sector generation is built on: hashing, the random number generator, the cell grid,
    /// region falloff, and the asteroid field's rocks, shapes and meshes. Pure checks with no scene or editor access,
    /// so they run anywhere.
    /// </summary>
    internal static class SectorSelfTest
    {
        // Values the hashing must keep producing. Every generated sector depends on them: if a change to the
        // hashing or the generator breaks these, every seed now builds a different sector.
        private const uint PinnedMix = 0x514E28B7u;
        private const uint PinnedCellSeed = 0x726681CFu;
        private const uint PinnedRandom = 0x3805EA2Cu;
        private const uint PinnedText = 0xB78702B4u;

        private const int UniformSamples = 1000000;
        private const int UniformBuckets = 100;

        // 99.9th percentile of chi-square with 99 degrees of freedom. A sound generator fails this once in a thousand seeds.
        private const double ChiSquareLimit = 148.2;

        /// <summary>The outcome of a run: one line per check, and the checks that failed.</summary>
        internal sealed class Result
        {
            public readonly StringBuilder Report = new StringBuilder();
            public readonly List<string> Failures = new List<string>();
            public int Checks;

            public bool Passed => Failures.Count == 0;

            public void Check(bool passed, string name, string detail)
            {
                ++Checks;
                Report.Append(passed ? "  pass  " : "  FAIL  ").Append(name).Append("  ·  ").AppendLine(detail);
                if (!passed)
                    Failures.Add($"{name}: {detail}");
            }

            public void Note(string name, string detail)
            {
                Report.Append("  info  ").Append(name).Append("  ·  ").AppendLine(detail);
            }
        }

        public static Result Run()
        {
            var result = new Result();
            CheckPinnedValues(result);
            CheckAvalanche(result);
            CheckUniformity(result);
            CheckRanges(result);
            CheckCellSeeds(result);
            CheckGrid(result);
            CheckRegion(result);
            CheckTurnedRegion(result);
            CheckLayout(result);
            CheckPlacement(result);
            CheckSunDirection(result);
            CheckBackdrop(result);
            CheckCruise(result);
            CheckField(result);
            CheckRockShapes(result);
            CheckIcosphere(result);
            CheckFieldOrder(result);
            MeasureSpeed(result);
            return result;
        }

        // Culling and the solid-rock pass leave a cell at the first rock too small to matter, which only holds while
        // every cell is kept largest first. Detail is judged from the camera's path, not only from where it is.
        private static void CheckFieldOrder(Result result)
        {
            const int Sorted = 257;
            var rocks = new NativeArray<FieldRock>(Sorted + 2, Allocator.Temp);
            var random = new SectorRandom(0xC0FFEEu);
            for (int i = 0; i < rocks.Length; ++i)
                rocks[i] = new FieldRock(float3.zero, 2f + random.Range(0, 40), random.NextUInt());

            uint before = rocks[0].Seed;
            uint after = rocks[Sorted + 1].Seed;
            var order = new LargestRockFirst();
            FieldSort.Sort(rocks, 1, Sorted, order);

            bool ordered = true;
            for (int i = 2; i <= Sorted; ++i)
                ordered &= !order.Before(rocks[i], rocks[i - 1]);

            bool untouched = rocks[0].Seed == before && rocks[Sorted + 1].Seed == after;
            rocks.Dispose();
            result.Check(ordered && untouched, "Field: cells sort largest first",
                $"{Sorted} rocks in order, equal sizes by seed, and the rocks either side of the run left alone");

            float3 ahead = new float3(0f, 0f, 300f);
            float beside = FieldRockShape.DetailDistance(new float3(40f, 0f, 200f), float3.zero, ahead);
            float behind = FieldRockShape.DetailDistance(new float3(0f, 0f, -100f), float3.zero, ahead);
            float beyond = FieldRockShape.DetailDistance(new float3(0f, 0f, 500f), float3.zero, ahead);
            float still = FieldRockShape.DetailDistance(new float3(30f, 0f, 40f), float3.zero, float3.zero);
            bool looksAhead = math.abs(beside - 40f) < 1e-3f && math.abs(behind - 100f) < 1e-3f && math.abs(beyond - 200f) < 1e-3f &&
                              math.abs(still - 50f) < 1e-3f;
            result.Check(looksAhead, "Field: detail looks ahead",
                $"a rock beside the path ahead counts as {beside:0} m away, one behind {behind:0} m, one past its end {beyond:0} m; " +
                $"at rest {still:0} m");
        }

        private static void CheckPinnedValues(Result result)
        {
            uint mix = SectorHash.Mix(1u);
            uint cell = SectorHash.Cell(12345u, new CellCoord(3, -2, 7), 1u);
            uint random = new SectorRandom(42u).NextUInt();
            uint text = SectorHash.Text("Polaris");

            result.Check(mix == PinnedMix, "Pinned hash", $"Mix(1) = 0x{mix:X8}");
            result.Check(cell == PinnedCellSeed, "Pinned cell seed", $"0x{cell:X8}");
            result.Check(random == PinnedRandom, "Pinned random value", $"0x{random:X8}");
            result.Check(text == PinnedText, "Pinned text hash", $"0x{text:X8}");
        }

        // Flipping any single input bit should flip about half of the 32 output bits.
        private static void CheckAvalanche(Result result)
        {
            long flipped = 0;
            int trials = 0;
            int worst = 32;
            for (uint value = 1; value < 20000; value += 7)
            {
                uint baseline = SectorHash.Mix(value);
                for (int bit = 0; bit < 32; ++bit)
                {
                    int count = CountBits(baseline ^ SectorHash.Mix(value ^ (1u << bit)));
                    flipped += count;
                    worst = Math.Min(worst, count);
                    ++trials;
                }
            }

            double average = (double)flipped / trials;
            result.Check(average > 15.5 && average < 16.5 && worst >= 3, "Hash avalanche",
                $"{average:0.00} of 32 bits flip on average, {worst} at worst");
        }

        private static void CheckUniformity(Result result)
        {
            var random = new SectorRandom(0xC0FFEEu);
            var buckets = new int[UniformBuckets];
            double sum = 0d;
            float min = float.MaxValue;
            float max = float.MinValue;

            for (int i = 0; i < UniformSamples; ++i)
            {
                float value = random.NextFloat();
                sum += value;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
                if (value >= 0f && value < 1f)
                    ++buckets[(int)(value * UniformBuckets)];
            }

            double expected = (double)UniformSamples / UniformBuckets;
            double chiSquare = 0d;
            foreach (int count in buckets)
                chiSquare += (count - expected) * (count - expected) / expected;

            double mean = sum / UniformSamples;
            result.Check(min >= 0f && max < 1f, "Random stays in range", $"min {min:0.0000000}, max {max:0.0000000}");
            result.Check(Math.Abs(mean - 0.5) < 0.002, "Random mean", $"{mean:0.00000} over {UniformSamples:N0} values");
            result.Check(chiSquare < ChiSquareLimit, "Random is evenly spread", $"chi-square {chiSquare:0.0}, limit {ChiSquareLimit:0.0}");
        }

        private static void CheckRanges(Result result)
        {
            var random = new SectorRandom(7u);
            bool inRange = true;
            bool hitLow = false;
            bool hitHigh = false;
            var min = new Vector3(-5f, 10f, 100f);
            var size = new Vector3(2f, 4f, 8f);

            for (int i = 0; i < 100000; ++i)
            {
                int whole = random.Range(-3, 4);
                hitLow |= whole == -3;
                hitHigh |= whole == 3;
                inRange &= whole >= -3 && whole < 4;

                float real = random.Range(2f, 5f);
                inRange &= real >= 2f && real < 5f;

                Vector3 point = random.NextInBox(min, size);
                inRange &= point.x >= min.x && point.x <= min.x + size.x &&
                    point.y >= min.y && point.y <= min.y + size.y &&
                    point.z >= min.z && point.z <= min.z + size.z;
            }

            result.Check(inRange && hitLow && hitHigh, "Ranges", "whole numbers, decimals and box points stay inside, and reach both ends");
            result.Check(random.Range(5, 5) == 5 && random.Range(5, 2) == 5, "Empty range", "returns its minimum");
        }

        // Cells of a Polaris-sized sector: no two share a seed, streams differ, and neighbours are unrelated.
        private static void CheckCellSeeds(Result result)
        {
            const int countX = 40, countY = 6, countZ = 40;
            const uint seed = 20261116u;

            var seen = new HashSet<uint>();
            int collisions = 0;
            int streamMatches = 0;
            double sumA = 0d, sumB = 0d, sumAB = 0d, sumAA = 0d, sumBB = 0d;
            int pairs = 0;

            for (int x = 0; x < countX; ++x)
            {
                for (int y = 0; y < countY; ++y)
                {
                    for (int z = 0; z < countZ; ++z)
                    {
                        var cell = new CellCoord(x, y, z);
                        uint cellSeed = SectorHash.Cell(seed, cell);
                        if (!seen.Add(cellSeed))
                            ++collisions;
                        if (SectorHash.Cell(seed, cell, 1u) == cellSeed)
                            ++streamMatches;

                        // First value of this cell against the first value of the next cell along x.
                        double a = new SectorRandom(cellSeed).NextFloat();
                        double b = new SectorRandom(SectorHash.Cell(seed, new CellCoord(x + 1, y, z))).NextFloat();
                        sumA += a;
                        sumB += b;
                        sumAB += a * b;
                        sumAA += a * a;
                        sumBB += b * b;
                        ++pairs;
                    }
                }
            }

            double covariance = sumAB / pairs - sumA / pairs * (sumB / pairs);
            double deviation = Math.Sqrt((sumAA / pairs - Math.Pow(sumA / pairs, 2)) * (sumBB / pairs - Math.Pow(sumB / pairs, 2)));
            double correlation = covariance / deviation;

            bool mirrored = SectorHash.Cell(seed, new CellCoord(1, 2, 3)) == SectorHash.Cell(seed, new CellCoord(3, 2, 1));
            bool reseeded = SectorHash.Cell(seed, new CellCoord(1, 2, 3)) == SectorHash.Cell(seed + 1u, new CellCoord(1, 2, 3));

            result.Check(collisions == 0, "Cell seeds are unique", $"{collisions} repeats in {seen.Count + collisions:N0} cells");
            result.Check(streamMatches == 0, "Streams are separate", $"{streamMatches} cells share a seed across streams");
            result.Check(Math.Abs(correlation) < 0.04, "Neighbouring cells are unrelated", $"correlation {correlation:0.0000}");
            result.Check(!mirrored && !reseeded, "Order and seed matter", "swapped coordinates and a changed seed give new values");
        }

        private static void CheckGrid(Result result)
        {
            var grid = new SectorGrid(new Vector3(40000f, 6000f, 40000f), 1000f);
            result.Check(grid.CountX == 40 && grid.CountY == 6 && grid.CountZ == 40 && grid.CellCount == 9600,
                "Grid size", $"{grid.CountX} x {grid.CountY} x {grid.CountZ} cells");

            int wrong = 0;
            for (int x = 0; x < grid.CountX; ++x)
            {
                for (int y = 0; y < grid.CountY; ++y)
                {
                    for (int z = 0; z < grid.CountZ; ++z)
                    {
                        var cell = new CellCoord(x, y, z);
                        if (grid.CellAt(grid.CellCentre(cell)) != cell || !grid.Contains(cell))
                            ++wrong;

                        // A cell owns its lowest corner.
                        if (grid.CellAt(grid.CellMin(cell)) != cell)
                            ++wrong;
                    }
                }
            }

            result.Check(wrong == 0, "Cells map back to themselves", $"{wrong} mismatches");

            CellCoord below = grid.CellAt(grid.Min - new Vector3(0.5f, 0.5f, 0.5f));
            CellCoord above = grid.CellAt(grid.Min + grid.Size + new Vector3(0.5f, 0.5f, 0.5f));
            result.Check(below == new CellCoord(-1, -1, -1) && !grid.Contains(below) &&
                    above == new CellCoord(grid.CountX, grid.CountY, grid.CountZ) && !grid.Contains(above),
                "Outside the grid", $"just below is {below}, just above is {above}, neither is contained");

            var uneven = new SectorGrid(new Vector3(2500f, 100f, 0f), 1000f);
            result.Check(uneven.CountX == 3 && uneven.CountY == 1 && uneven.CountZ == 1 && Mathf.Approximately(uneven.Min.x, -1500f),
                "Uneven sizes round up", $"{uneven.CountX} x {uneven.CountY} x {uneven.CountZ} cells, centred");
        }

        private static void CheckRegion(Result result)
        {
            var centre = new Vector3(100f, -50f, 20f);
            var radii = new Vector3(1000f, 200f, 500f);
            var region = new SectorRegion("Test", SectorRegionKind.AsteroidField, centre, radii, 0.8f, 0.5f);

            result.Check(Mathf.Approximately(region.StrengthAt(centre), 0.8f), "Region centre", "full density at the centre");
            result.Check(region.StrengthAt(centre + new Vector3(1000f, 0f, 0f)) == 0f &&
                    region.StrengthAt(centre + new Vector3(0f, 201f, 0f)) == 0f &&
                    region.StrengthAt(centre + new Vector3(0f, 199f, 0f)) > 0f,
                "Region edge", "empty on and beyond the surface, on each axis");

            // Strength never rises on the way out, never jumps, and stays between 0 and the density.
            bool monotonic = true;
            bool bounded = true;
            float largestStep = 0f;
            float previous = region.StrengthAt(centre);
            var direction = new Vector3(0.6f, 0.1f, 0.3f);
            for (int i = 1; i <= 4000; ++i)
            {
                float strength = region.StrengthAt(centre + direction * (i * 0.5f));
                monotonic &= strength <= previous + 0.000001f;
                bounded &= strength >= 0f && strength <= 0.8f;
                largestStep = Math.Max(largestStep, previous - strength);
                previous = strength;
            }

            result.Check(monotonic && bounded, "Region fades outwards", "never rises, stays between 0 and its density");
            result.Check(largestStep < 0.01f, "Region fade is smooth", $"largest step {largestStep:0.00000} per half metre");

            var hard = new SectorRegion("Hard", SectorRegionKind.AsteroidField, centre, radii, 0.5f, 0f);
            var flat = new SectorRegion("Flat", SectorRegionKind.AsteroidField, centre, new Vector3(1000f, 0f, 500f), 0.5f, 0.5f);
            result.Check(Mathf.Approximately(hard.StrengthAt(centre + new Vector3(990f, 0f, 0f)), 0.5f) && flat.StrengthAt(centre) == 0f,
                "Region edge cases", "no falloff gives a hard edge, a zero radius gives an empty region");
        }

        // A region turned a quarter is the same region with its long and short axes swapped, and its bounding box
        // holds every point it reaches at any turn.
        private static void CheckTurnedRegion(Result result)
        {
            var centre = new Vector3(300f, 40f, -200f);
            var radii = new Vector3(1000f, 200f, 400f);
            var turned = new SectorRegion("Turned", SectorRegionKind.AsteroidField, centre, radii, 90f, 0.8f, 0.5f, default);
            var swapped = new SectorRegion("Swapped", SectorRegionKind.AsteroidField, centre, new Vector3(radii.z, radii.y, radii.x), 0.8f, 0.5f);

            var random = new SectorRandom(31u);
            float worst = 0f;
            for (int i = 0; i < 20000; ++i)
            {
                Vector3 point = random.NextInBox(centre - radii * 1.2f, radii * 2.4f);
                worst = Math.Max(worst, Math.Abs(turned.StrengthAt(point) - swapped.StrengthAt(point)));
            }

            result.Check(worst < 1e-4f, "Turned region", $"a quarter turn matches the swapped axes to within {worst:0.000000}");

            bool contained = true;
            bool forwardMatches = true;
            for (int step = 0; step < 72; ++step)
            {
                RegionVolume volume = RegionVolume.Create(centre, radii, step * 5f - 180f, 1f, 0f, SectorRegionKind.AsteroidField);
                for (int i = 0; i < 64; ++i)
                {
                    float angle = i * (2f * Mathf.PI / 64f);
                    Vector3 surface = (Vector3)volume.Centre + (Vector3)volume.RotateToSector(new float3(Mathf.Cos(angle) * radii.x, 0f, Mathf.Sin(angle) * radii.z));
                    float3 offset = math.abs((float3)surface - volume.Centre);
                    contained &= math.all(offset <= volume.Extent + 0.01f);
                }

                Vector3 forward = Yaw(volume.Yaw) * Vector3.forward;
                forwardMatches &= Vector3.Distance(forward, volume.RotateToSector(new float3(0f, 0f, 1f))) < 1e-4f;
            }

            result.Check(contained && forwardMatches, "Turned region bounds",
                "every surface point lies inside the bounding box at every turn, and turns match Unity's yaw");
        }

        // Three regions like a real sector's: a fixed clearing round the arrival point and two fields that vary.
        private static SectorRegion[] TestLayoutRegions()
        {
            return new[]
            {
                new SectorRegion("Arrival", SectorRegionKind.Clearing, Vector3.zero, new Vector3(2500f, 2500f, 2500f), 1f, 0.5f),
                new SectorRegion("Belt", SectorRegionKind.AsteroidField, new Vector3(5000f, 0f, 8000f), new Vector3(10000f, 1500f, 7000f), 0f,
                    0.8f, 0.5f, new RegionVariation(180f, new Vector3(1000f, 300f, 1000f), 30f, 0.15f)),
                new SectorRegion("Wrecks", SectorRegionKind.DebrisField, new Vector3(-10000f, 400f, -5000f), new Vector3(5000f, 1200f, 5000f), 0f,
                    0.6f, 0.4f, new RegionVariation(180f, new Vector3(2000f, 300f, 2000f), 180f, 0.25f))
            };
        }

        private static void CheckLayout(Result result)
        {
            const int seeds = 500;
            var grid = new SectorGrid(new Vector3(40000f, 6000f, 40000f), 1000f);
            SectorRegion[] authored = TestLayoutRegions();
            var first = new SectorLayout();
            var second = new SectorLayout();

            first.Build(authored, grid, 42u, true);
            second.Build(authored, grid, 42u, true);
            bool same = SameLayout(first, second);
            second.Build(authored, grid, 43u, true);
            bool moved = Vector3.Distance(first[1].Centre, second[1].Centre) > 100f && Math.Abs(first[1].Yaw - second[1].Yaw) > 0.1f;
            result.Check(same && moved, "Layout: same seed, same layout", "identical twice from one seed; a new seed moves and turns the fields");

            second.Build(authored, grid, 42u, false);
            bool authoredKept = true;
            for (int i = 0; i < authored.Length; ++i)
                authoredKept &= SameVolume(second[i], authored[i].Volume);
            result.Check(authoredKept && SameVolume(first[0], authored[0].Volume), "Layout: fixed regions stay as authored",
                "with Vary Layout off nothing moves, and a region with no variation never moves");

            float3 gridMin = grid.Min;
            float3 gridMax = gridMin + (float3)grid.Size;
            int outside = 0;
            int arrivalNotClear = 0;
            float nearestBelt = float.MaxValue;
            float farthestBelt = 0f;
            float lowestBearing = 360f;
            float highestBearing = 0f;
            for (uint seed = 1; seed <= seeds; ++seed)
            {
                first.Build(authored, grid, seed * 7919u, true);
                for (int i = 0; i < first.Count; ++i)
                {
                    RegionVolume region = first[i];
                    if (math.any(region.Centre - region.Extent < gridMin - 0.5f) || math.any(region.Centre + region.Extent > gridMax + 0.5f))
                        ++outside;
                }

                if (first.DensityAt(Vector3.zero, out _) > 0f || first.DensityAt(new Vector3(800f, 0f, 800f), out _) > 0f)
                    ++arrivalNotClear;

                Vector3 belt = first[1].Centre;
                float distance = new Vector2(belt.x, belt.z).magnitude;
                nearestBelt = Math.Min(nearestBelt, distance);
                farthestBelt = Math.Max(farthestBelt, distance);
                float bearing = Mathf.Repeat(Mathf.Atan2(belt.x, belt.z) * Mathf.Rad2Deg, 360f);
                lowestBearing = Math.Min(lowestBearing, bearing);
                highestBearing = Math.Max(highestBearing, bearing);
            }

            result.Check(outside == 0, "Layout: regions stay in the sector", $"{outside} of {seeds * authored.Length} varied regions reach outside");
            result.Check(arrivalNotClear == 0, "Layout: the arrival point stays clear", $"{arrivalNotClear} of {seeds} layouts put field at the arrival point");
            result.Check(lowestBearing < 20f && highestBearing > 340f && nearestBelt > 6000f && farthestBelt < 12000f, "Layout: fields swing round the arrival point",
                $"belt centre from {nearestBelt / 1000f:0.0} to {farthestBelt / 1000f:0.0} km out, at bearings {lowestBearing:0}° to {highestBearing:0}°");

            first.Build(authored, grid, 5u, true);
            int before = first.Count;
            var spot = new Vector3(6000f, 0f, 6000f);
            first.AddClearing("Spot", spot, 400f);
            result.Check(first.Count == before + 1 && first.AuthoredCount == authored.Length && first.DensityAt(spot, out _) == 0f &&
                         first.DensityAt(spot + new Vector3(150f, 0f, 0f), out _) == 0f,
                "Layout: placed content clears its spot", "a clearing added after the regions empties the field round it");
        }

        private static void CheckPlacement(Result result)
        {
            const int seeds = 300;
            var grid = new SectorGrid(new Vector3(40000f, 6000f, 40000f), 1000f);
            SectorRegion[] authored = TestLayoutRegions();
            var layout = new SectorLayout();
            var taken = new List<Vector3>();

            var gate = new PlacementSettings
            {
                MinDistance = 4000f, MaxDistance = 9000f, MaxHeight = 600f, MinDensity = 0f, MaxDensity = 0.3f,
                Spacing = 3000f, KeepClear = 400f, FaceArrival = true
            };
            var pickup = new PlacementSettings
            {
                MinDistance = 1200f, MaxDistance = 8000f, MaxHeight = 800f, MinDensity = 0f, MaxDensity = 1f,
                Spacing = 400f, KeepClear = 0f, FaceArrival = false
            };

            int placed = 0;
            int missed = 0;
            int broken = 0;
            int notFacing = 0;
            int notClear = 0;
            for (uint seed = 1; seed <= seeds; ++seed)
            {
                layout.Build(authored, grid, seed, true);
                taken.Clear();
                for (int i = 0; i < 13; ++i)
                {
                    PlacementSettings settings = i == 0 ? gate : pickup;
                    bool met = SectorPlacer.TryPlace(layout.Set, grid, seed, i, settings, taken, out Vector3 spot, out float heading);
                    ++placed;
                    if (!met)
                    {
                        ++missed;
                    }
                    else
                    {
                        float distance = spot.magnitude;
                        float flat = new Vector2(spot.x, spot.z).magnitude;
                        float density = layout.DensityAt(spot, out _);
                        float nearest = float.MaxValue;
                        foreach (Vector3 other in taken)
                            nearest = Math.Min(nearest, Vector3.Distance(other, spot));

                        bool ok = flat >= settings.MinDistance - 0.5f && flat <= settings.MaxDistance + 0.5f && distance > 0f &&
                                  Math.Abs(spot.y) <= settings.MaxHeight + 0.01f &&
                                  density >= settings.MinDensity - 1e-5f && density <= settings.MaxDensity + 1e-5f &&
                                  nearest >= settings.Spacing - 0.01f &&
                                  SectorPlacer.Miss(layout.Set, grid, settings, taken, spot) == 0f;
                        if (!ok)
                            ++broken;

                        if (settings.FaceArrival)
                        {
                            Vector3 facing = Yaw(heading) * Vector3.forward;
                            if (Vector3.Dot(facing, new Vector3(-spot.x, 0f, -spot.z).normalized) < 0.999f)
                                ++notFacing;
                        }
                    }

                    taken.Add(spot);
                    layout.AddClearing("Placed", spot, settings.KeepClear);
                    if (settings.KeepClear > 0f && layout.DensityAt(spot, out _) > 0f)
                        ++notClear;
                }
            }

            result.Check(broken == 0 && notFacing == 0 && notClear == 0, "Placement: spots meet their rule",
                $"{placed - missed} spots checked for distance, height, density and spacing; {broken} broke it, {notFacing} gates faced away");
            result.Check(missed <= placed / 200, "Placement: rules can be met", $"{missed} of {placed} pieces fell back to their closest miss");

            layout.Build(authored, grid, 99u, true);
            taken.Clear();
            SectorPlacer.TryPlace(layout.Set, grid, 99u, 3, pickup, taken, out Vector3 a, out float headingA);
            SectorPlacer.TryPlace(layout.Set, grid, 99u, 3, pickup, taken, out Vector3 b, out float headingB);
            SectorPlacer.TryPlace(layout.Set, grid, 100u, 3, pickup, taken, out Vector3 c, out _);
            SectorPlacer.TryPlace(layout.Set, grid, 99u, 4, pickup, taken, out Vector3 d, out _);
            result.Check(a == b && headingA == headingB && a != c && a != d, "Placement: same seed, same spot",
                "a piece lands in the same spot twice; a new seed or another piece moves it");
        }

        // Each run's sun sits beside the opening view, never in it or behind it, within its band round the plane, from
        // either side.
        private static void CheckSunDirection(Result result)
        {
            const int seeds = 2000;
            const float clear = 70f;
            const float furthest = 125f;
            const float band = 50f;
            Vector3 view = new Vector3(0.1f, 0.15f, 1f).normalized;

            int outOfRange = 0;
            int outOfBand = 0;
            int notUnit = 0;
            float nearest = 180f;
            float widest = 0f;
            float highest = 0f;
            int left = 0;
            int right = 0;
            for (uint seed = 1; seed <= seeds; ++seed)
            {
                Vector3 sun = SpaceLighting.PickDirection(seed * 2654435761u, view, clear, furthest, band);
                float angle = Vector3.Angle(sun, view);
                float elevation = Mathf.Asin(Mathf.Clamp(sun.y, -1f, 1f)) * Mathf.Rad2Deg;
                nearest = Math.Min(nearest, angle);
                widest = Math.Max(widest, angle);
                highest = Math.Max(highest, Math.Abs(elevation));
                outOfRange += angle < clear - 0.01f || angle > furthest + 0.01f ? 1 : 0;
                outOfBand += Math.Abs(elevation) > band + 0.01f ? 1 : 0;
                notUnit += Math.Abs(sun.magnitude - 1f) > 1e-4f ? 1 : 0;
                left += sun.x < -0.3f ? 1 : 0;
                right += sun.x > 0.3f ? 1 : 0;
            }

            result.Check(outOfRange == 0 && outOfBand == 0 && notUnit == 0, "Sun: beside the opening view",
                $"{nearest:0.0}° to {widest:0.0}° from the view ({clear:0} to {furthest:0} allowed), highest {highest:0.0}° from the plane (at most {band:0})");
            result.Check(left > seeds / 4 && right > seeds / 4, "Sun: from either side",
                $"{left} to the left and {right} to the right, of {seeds} runs");

            Vector3 a = SpaceLighting.PickDirection(77u, view, clear, furthest, band);
            Vector3 b = SpaceLighting.PickDirection(77u, view, clear, furthest, band);
            Vector3 c = SpaceLighting.PickDirection(78u, view, clear, furthest, band);
            Vector3 impossible = SpaceLighting.PickDirection(5u, view, 179f, 180f, band);
            result.Check(a == b && a != c && Vector3.Angle(impossible, view) > 90f, "Sun: same seed, same sun",
                "a seed turns the sun the same way twice, a new seed elsewhere, and an impossible range still faces away");
        }

        // Every run's planets keep clear of the sun and of each other, moons sit beside the primary, the rest stay in their
        // band, no planet plays a part it is not given or repeats while others are free, a parent never moves when its
        // children change, and the same seed always gives the same sky.
        private static void CheckBackdrop(Result result)
        {
            const int seeds = 1000;
            const float tolerance = 0.01f;
            const float sunRadius = 34f; // Polaris's sun: 60° across, with a 4° corona.
            Vector3 view = new Vector3(0.1f, 0.15f, 1f).normalized;

            // A full system, moons included, so every rule is exercised; Polaris itself runs two planets.
            BackdropSettings settings = BackdropSettings.Default;
            settings.MaxMoons = 2;
            settings.MinDistant = 0;
            var entries = new List<PlanetEntry>
            {
                new PlanetEntry("Ocean A", "a", 500f, BodyRoles.Primary | BodyRoles.Distant, 1f),
                new PlanetEntry("Ocean B", "b", 500f, BodyRoles.Primary | BodyRoles.Distant, 1f),
                new PlanetEntry("Ocean C", "c", 500f, BodyRoles.Primary | BodyRoles.Distant, 2f),
                new PlanetEntry("Dry A", "d", 500f, BodyRoles.All, 1f),
                new PlanetEntry("Dry B", "e", 500f, BodyRoles.All, 1f),
                new PlanetEntry("Airless", "f", 500f, BodyRoles.Moon | BodyRoles.Distant, 1f),
                new PlanetEntry("Never", "g", 500f, BodyRoles.All, 0f)
            };

            var plans = new List<BackdropPlan>();
            var earlier = new List<BackdropPlan>();
            int primaries = 0, misses = 0, moons = 0, distant = 0;
            int ruleBreaks = 0, overlaps = 0, sunHits = 0, moonsAway = 0, outOfBand = 0, repeats = 0, wrongRole = 0;
            float nearestSunGap = 180f;
            for (uint seed = 1; seed <= seeds; ++seed)
            {
                Vector3 sun = SpaceLighting.PickDirection(seed * 2654435761u, view, 70f, 125f, 50f);
                BackdropLayout.Plan(seed * 40503u + 7u, settings, sun, sunRadius, view, entries, plans);

                for (int i = 0; i < plans.Count; ++i)
                {
                    BackdropPlan plan = plans[i];
                    PlanetEntry entry = entries[plan.Entry];
                    wrongRole += entry.CanPlay(plan.Role) ? 0 : 1;

                    float sunGap = Vector3.Angle(plan.Direction, sun) - sunRadius - plan.AngularRadius;
                    nearestSunGap = Math.Min(nearestSunGap, sunGap);
                    sunHits += sunGap < settings.SunClearance - tolerance ? 1 : 0;

                    if (plan.Role == BodyRoles.Primary)
                    {
                        ++primaries;
                        misses += plan.MetRule ? 0 : 1;
                    }
                    else if (plan.Role == BodyRoles.Moon)
                    {
                        ++moons;
                        float gap = Vector3.Angle(plan.Direction, plans[0].Direction) - plans[0].AngularRadius;
                        moonsAway += plans[0].Role != BodyRoles.Primary || gap < settings.MoonMinGap - tolerance ||
                                     gap > settings.MoonMaxGap + tolerance ? 1 : 0;
                    }
                    else
                    {
                        ++distant;
                    }

                    if (plan.Role != BodyRoles.Moon)
                        outOfBand += Math.Abs(BackdropLayout.Elevation(plan.Direction)) > settings.MaxElevation + tolerance ? 1 : 0;

                    earlier.Clear();
                    for (int j = 0; j < i; ++j)
                    {
                        earlier.Add(plans[j]);
                        float apart = Vector3.Angle(plan.Direction, plans[j].Direction);
                        overlaps += apart < plan.AngularRadius + plans[j].AngularRadius + settings.BodyClearance - tolerance ? 1 : 0;
                        repeats += plans[j].Entry == plan.Entry ? 1 : 0;
                    }

                    if (plan.MetRule && BackdropLayout.Miss(plan.Direction, plan.AngularRadius, plan.Role, settings, sun, sunRadius, view, earlier) > tolerance)
                        ++ruleBreaks;
                }
            }

            result.Check(primaries == seeds && misses == 0 && ruleBreaks == 0, "Backdrop: every rule met",
                $"{primaries} primaries in {seeds} runs, {misses} closest misses, {ruleBreaks} bodies breaking a rule");
            result.Check(sunHits == 0 && overlaps == 0, "Backdrop: clear of the sun and each other",
                $"nearest gap to the sun's edge {nearestSunGap:0.0}° ({settings.SunClearance:0} needed), {overlaps} overlapping pairs");
            result.Check(moons > 0 && distant > 0 && moonsAway == 0 && outOfBand == 0, "Backdrop: moons beside the primary",
                $"{moons} moons, {moonsAway} away from the primary; {distant} distant planets; {outOfBand} out of the {settings.MaxElevation:0}° band");
            result.Check(wrongRole == 0 && repeats == 0, "Backdrop: planets play their parts",
                $"{wrongRole} in a part they are not given (weight 0 included), {repeats} repeated in a run");

            // Polaris's own rules: exactly two planets every run, the primary and one other, both meeting every rule.
            BackdropSettings polaris = BackdropSettings.Default;
            int notTwo = 0, twoMissed = 0, twoSame = 0;
            for (uint seed = 1; seed <= seeds; ++seed)
            {
                Vector3 sun = SpaceLighting.PickDirection(seed * 2654435761u, view, 70f, 125f, 50f);
                BackdropLayout.Plan(seed * 40503u + 7u, polaris, sun, sunRadius, view, entries, plans);
                bool two = plans.Count == 2 && plans[0].Role == BodyRoles.Primary && plans[1].Role == BodyRoles.Distant;
                notTwo += two ? 0 : 1;
                twoMissed += two && (!plans[0].MetRule || !plans[1].MetRule) ? 1 : 0;
                twoSame += two && plans[0].Entry == plans[1].Entry ? 1 : 0;
            }

            result.Check(notTwo == 0 && twoMissed == 0 && twoSame == 0, "Backdrop: two planets a run",
                $"{seeds - notTwo} of {seeds} runs have the primary and one distant planet, {twoMissed} settled for a closest miss, " +
                $"{twoSame} used the same planet twice");

            Vector3 fixedSun = SpaceLighting.PickDirection(9u, view, 70f, 125f, 50f);
            var a = new List<BackdropPlan>();
            var b = new List<BackdropPlan>();
            var c = new List<BackdropPlan>();
            BackdropLayout.Plan(1234u, settings, fixedSun, sunRadius, view, entries, a);
            BackdropLayout.Plan(1234u, settings, fixedSun, sunRadius, view, entries, b);
            BackdropLayout.Plan(1235u, settings, fixedSun, sunRadius, view, entries, c);

            BackdropSettings noMoons = settings;
            noMoons.MaxMoons = 0;
            var parentOnly = new List<BackdropPlan>();
            BackdropLayout.Plan(1234u, noMoons, fixedSun, sunRadius, view, entries, parentOnly);
            bool parentKept = a.Count > 0 && parentOnly.Count > 0 && SamePlan(a[0], parentOnly[0]);

            var empty = new List<BackdropPlan>();
            BackdropLayout.Plan(1234u, settings, fixedSun, sunRadius, view, new List<PlanetEntry>(), empty);

            result.Check(SameSystem(a, b) && !SameSystem(a, c) && parentKept && empty.Count == 0, "Backdrop: same seed, same sky",
                "a seed gives the same system twice and another seed a new one; taking the moons away leaves the primary where it was; no planets, no bodies");

            var thirty = new BackdropPlan(BodyRoles.Primary, 0, Vector3.forward, 30f, Vector3.up, 0f, 0f, true);
            float distance = thirty.DistanceFor(500f);
            result.Check(Math.Abs(distance - 1931.85f) < 0.5f, "Backdrop: size sets distance",
                $"a 500 km planet 30° across stands {distance:0} km away (1,932 expected)");
        }

        // The cruise drive's maths, and the trips it was tuned for: from Polaris's belt to Ixara's orbit and back, and out to
        // a smaller, further planet. The trip follows the drive's own rule (CruiseDrive.Cruise): ease towards the target
        // speed, step, and stop at a planet's floor.
        private static void CheckCruise(Result result)
        {
            CruiseSettings settings = CruiseSettings.Default;
            bool speeds = CruiseMath.TargetSpeed(0.0, settings) == settings.MinSpeed &&
                          CruiseMath.TargetSpeed(1e12, settings) == settings.MaxSpeed &&
                          CruiseMath.TargetSpeed(1e6, settings) > CruiseMath.TargetSpeed(1e5, settings);

            var boxMin = new double3(-20000.0, -3000.0, -20000.0);
            var boxMax = new double3(20000.0, 3000.0, 20000.0);
            bool boxes = CruiseMath.DistanceToBox(double3.zero, boxMin, boxMax) == 0.0 &&
                         Math.Abs(CruiseMath.DistanceToBox(new double3(23000.0, 0.0, 24000.0), boxMin, boxMax) - 5000.0) < 1e-6;

            var centre = new double3(0.0, 0.0, 10000.0);
            bool spheres = Math.Abs(CruiseMath.DistanceToSphere(double3.zero, new double3(0, 0, 1), centre, 1000.0) - 9000.0) < 1e-6 &&
                           double.IsPositiveInfinity(CruiseMath.DistanceToSphere(double3.zero, new double3(0, 1, 0), centre, 1000.0)) &&
                           CruiseMath.DistanceToSphere(centre, new double3(1, 0, 0), centre, 1000.0) == 0.0;

            result.Check(speeds && boxes && spheres, "Cruise: speed and distances",
                "speed holds to its limits and rises with clearance; distances to the sector's box and to a sphere are exact");

            // Ixara: 500 km in radius, 30° across, so 1,932 km out. Halonix: 500 km, 5° across, 11,464 km out.
            const double radius = 500000.0;
            var direction = math.normalize(new double3(0.6, 0.1, 0.79));
            double3 ixara = direction * (radius / Math.Sin(15.0 * Math.PI / 180.0));
            double3 halonix = math.normalize(new double3(-0.7, -0.05, 0.4)) * (radius / Math.Sin(2.5 * Math.PI / 180.0));
            var belt = new double3(8000.0, 0.0, 5000.0);
            const double homeMargin = 10000.0;

            Trip outbound = Fly(belt, ixara, radius, boxMin, boxMax, homeMargin, false, settings);
            Trip homeward = Fly(outbound.End, double3.zero, 0.0, boxMin, boxMax, homeMargin, true, settings, ixara, radius);
            Trip further = Fly(belt, halonix, radius, boxMin, boxMax, homeMargin, false, settings);

            result.Check(outbound.Arrived && outbound.Seconds < 60.0 && outbound.LeftHomeAt < 15.0 && Math.Abs(outbound.Altitude - settings.DropHeight) < 1.0,
                "Cruise: Polaris to above Ixara",
                $"out of reach of the sector in {outbound.LeftHomeAt:0.0} s, dropped out {outbound.Altitude / 1000.0:0.0} km above it after {outbound.Seconds:0.0} s, " +
                $"fastest {outbound.PeakSpeed / 1000.0:N0} km/s");
            result.Check(homeward.Arrived && homeward.Seconds < 60.0, "Cruise: Ixara back to Polaris",
                $"within {homeMargin / 1000.0:0} km of the sector after {homeward.Seconds:0.0} s");
            result.Check(further.Arrived && further.Seconds < 90.0 && further.PeakSpeed <= settings.MaxSpeed,
                "Cruise: out to a further planet",
                $"above a planet 11,464 km out after {further.Seconds:0.0} s, fastest {further.PeakSpeed / 1000.0:N0} km/s");

            // A planet's pull: full at the surface, a quarter at twice the radius, never stronger below the surface.
            double atSurface = PlanetPhysics.GravityAt(radius, radius, 6.0);
            double atTwice = PlanetPhysics.GravityAt(radius * 2.0, radius, 6.0);
            double below = PlanetPhysics.GravityAt(radius * 0.5, radius, 6.0);
            double atDrop = PlanetPhysics.GravityAt(radius + settings.DropHeight, radius, 6.0);
            result.Check(Math.Abs(atSurface - 6.0) < 1e-9 && Math.Abs(atTwice - 1.5) < 1e-9 && below == atSurface &&
                         PlanetPhysics.GravityAt(radius, 0.0, 6.0) == 0.0 && atDrop < atSurface && atDrop > 5.9,
                "Ground: gravity",
                $"{atSurface:0.00} m/s² at the surface, {atDrop:0.00} at the drop height, {atTwice:0.00} at twice the radius, none without a planet");
        }

        private struct Trip
        {
            public bool Arrived;
            public double Seconds;
            public double LeftHomeAt;
            public double PeakSpeed;
            public double Altitude;
            public double3 End;
        }

        // Flies straight at the target: a planet's floor, or with home set, the sector's reach. A planet passed along the
        // way still sets the clearance, as it does for the drive.
        private static Trip Fly(double3 start, double3 target, double targetRadius, double3 boxMin, double3 boxMax, double homeMargin,
            bool home, in CruiseSettings settings, double3 otherCentre = default, double otherRadius = 0.0)
        {
            const double step = 1.0 / 60.0;
            const double limit = 600.0;
            double3 direction = math.normalize(target - start);
            double3 position = start;
            double speed = 0.0;
            var trip = new Trip { LeftHomeAt = double.NaN };

            for (double time = 0.0; time < limit; time += step)
            {
                double toBox = CruiseMath.DistanceToBox(position, boxMin, boxMax);
                if (double.IsNaN(trip.LeftHomeAt) && toBox > homeMargin)
                    trip.LeftHomeAt = time;

                double clearance = toBox;
                if (!home)
                    clearance = Math.Min(clearance, CruiseMath.Altitude(position, target, targetRadius));
                else if (otherRadius > 0.0)
                    clearance = Math.Min(clearance, CruiseMath.Altitude(position, otherCentre, otherRadius));

                speed = CruiseMath.Ease(speed, CruiseMath.TargetSpeed(clearance, settings), settings.Response, step);
                trip.PeakSpeed = Math.Max(trip.PeakSpeed, speed);
                double move = speed * step;

                if (!home)
                {
                    double toFloor = CruiseMath.DistanceToSphere(position, direction, target, targetRadius + settings.DropHeight);
                    if (toFloor <= move)
                    {
                        position += direction * toFloor;
                        trip.Arrived = true;
                        trip.Seconds = time + step;
                        break;
                    }
                }

                position += direction * move;
                if (home && CruiseMath.DistanceToBox(position, boxMin, boxMax) <= homeMargin)
                {
                    trip.Arrived = true;
                    trip.Seconds = time + step;
                    break;
                }
            }

            trip.End = position;
            trip.Altitude = home ? 0.0 : CruiseMath.Altitude(position, target, targetRadius);
            return trip;
        }

        private static bool SameSystem(List<BackdropPlan> a, List<BackdropPlan> b)
        {
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; ++i)
            {
                if (!SamePlan(a[i], b[i]))
                    return false;
            }

            return true;
        }

        private static bool SamePlan(in BackdropPlan a, in BackdropPlan b)
        {
            return a.Role == b.Role && a.Entry == b.Entry && a.Direction == b.Direction && a.Size == b.Size &&
                   a.SpinAxis == b.SpinAxis && a.SpinPhase == b.SpinPhase && a.SpinSpeed == b.SpinSpeed;
        }

        // The rotation Quaternion.Euler(0, degrees, 0) gives, built in managed code so the check also runs outside Unity.
        private static Quaternion Yaw(float degrees)
        {
            float half = degrees * Mathf.Deg2Rad * 0.5f;
            return new Quaternion(0f, Mathf.Sin(half), 0f, Mathf.Cos(half));
        }

        private static bool SameLayout(SectorLayout a, SectorLayout b)
        {
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; ++i)
            {
                if (!SameVolume(a[i], b[i]))
                    return false;
            }

            return true;
        }

        private static bool SameVolume(in RegionVolume a, in RegionVolume b)
        {
            return math.all(a.Centre == b.Centre) && math.all(a.Radii == b.Radii) && a.Yaw == b.Yaw && a.Density == b.Density &&
                   a.Falloff == b.Falloff && a.Kind == b.Kind;
        }

        private static void CheckField(Result result)
        {
            const float cellSize = 1000f;
            const uint seed = 777u;
            var grid = new SectorGrid(new Vector3(8000f, 8000f, 8000f), cellSize);
            var centre = new CellCoord(3, 3, 3);
            FieldSettings full = FieldSettings.Create(1000f, 4f, 1200f, 100f, 2f, 0.3f, 1.3f, 8, 4f, cellSize);

            // With no large band, nothing keeps rocks out, so counts follow the density alone.
            FieldSettings small = FieldSettings.Create(1000f, 4f, 100f, 100f, 2f, 0.3f, 1.3f, 8, 4f, cellSize);

            var belt = new SectorRegionSet(new[] { Field(SectorRegionKind.AsteroidField, 1f) });
            var half = new SectorRegionSet(new[] { Field(SectorRegionKind.AsteroidField, 0.5f) });
            var debris = new SectorRegionSet(new[] { Field(SectorRegionKind.DebrisField, 1f) });
            var cleared = new SectorRegionSet(new[]
            {
                Field(SectorRegionKind.AsteroidField, 1f),
                new SectorRegion("Clearing", SectorRegionKind.Clearing, grid.CellCentre(centre), Vector3.one * 2000f, 1f, 0f)
            });

            List<FieldRock> first = Generate(full, belt, grid, seed, centre);
            List<FieldRock> again = Generate(full, belt, grid, seed, centre);
            bool same = first.Count == again.Count;
            for (int i = 0; same && i < first.Count; ++i)
                same = first[i].Position.Equals(again[i].Position) && first[i].Radius == again[i].Radius && first[i].Seed == again[i].Seed;
            List<FieldRock> reseeded = Generate(full, belt, grid, seed + 1u, centre);
            bool differs = reseeded.Count != first.Count || !reseeded[0].Position.Equals(first[0].Position);
            result.Check(same && differs, "Field: same seed, same rocks", $"{first.Count} rocks, identical twice; a new seed moves them");

            int fullCount = Generate(small, belt, grid, seed, centre).Count;
            int halfCount = Generate(small, half, grid, seed, centre).Count;
            float expected = small.ExpectedPerCell;
            result.Check(Within(fullCount, expected, 0.02f) && Within(halfCount, expected * 0.5f, 0.1f), "Field: density sets the count",
                $"{fullCount} rocks at full density and {halfCount} at half, of {expected:0} and {expected * 0.5f:0} expected");

            int inClearing = Generate(full, cleared, grid, seed, centre).Count;
            int inDebris = Generate(full, debris, grid, seed, centre).Count;
            int outside = Generate(full, belt, grid, seed, new CellCoord(-5, 3, 3)).Count;
            result.Check(inClearing == 0 && inDebris == 0 && outside == 0, "Field: rock only in asteroid fields",
                $"{inClearing} in a clearing, {inDebris} in a debris field, {outside} outside the grid");

            CheckKeepOut(result, full, belt, grid, seed, centre, first);
            CheckSizes(result, full, belt, grid, seed, centre);
            CheckShelter(result, full, grid, seed, centre);
        }

        // Placed content shelters its spot: no rock reaches into it, from its own cell or a neighbour, giants included.
        // Rocks reach at most 1,061 m here, lumps included, so the spot's cell and its neighbours hold every rock that could.
        private static void CheckShelter(Result result, in FieldSettings settings, in SectorGrid grid, uint seed, CellCoord centre)
        {
            const float shelter = 150f;
            SectorRegion[] authored = { Field(SectorRegionKind.AsteroidField, 1f) };
            Vector3 spot = grid.CellCentre(centre);

            var open = new SectorLayout();
            open.Build(authored, grid, seed, false);
            var sheltered = new SectorLayout();
            sheltered.Build(authored, grid, seed, false);
            sheltered.AddClearing("Content", spot, shelter);

            int checkedRocks = 0;
            int reaching = 0;
            int wouldReach = 0;
            for (int z = -1; z <= 1; ++z)
            {
                for (int y = -1; y <= 1; ++y)
                {
                    for (int x = -1; x <= 1; ++x)
                    {
                        var cell = new CellCoord(centre.X + x, centre.Y + y, centre.Z + z);
                        foreach (FieldRock rock in Generate(settings, sheltered.Set, grid, seed, cell))
                        {
                            ++checkedRocks;
                            if (Reaches(settings, rock, spot, shelter))
                                ++reaching;
                        }

                        foreach (FieldRock rock in Generate(settings, open.Set, grid, seed, cell))
                        {
                            if (Reaches(settings, rock, spot, shelter))
                                ++wouldReach;
                        }
                    }
                }
            }

            result.Check(reaching == 0 && wouldReach > 0, "Field: placed content is never buried",
                $"{checkedRocks:N0} rocks round a {shelter:0} m shelter, {reaching} reach into it; {wouldReach} would without it");
        }

        // Measured against the shape's own bound, not through FieldGeneration.Reach, so a change there cannot pass unseen.
        private static bool Reaches(in FieldSettings settings, in FieldRock rock, Vector3 spot, float shelter)
        {
            return Vector3.Distance(rock.Position, spot) < shelter + rock.Radius * (1f + settings.Stretch) * FieldRockShape.MaxRadius;
        }

        // No small rock may sit inside the reach of a large one from its own cell or a neighbour.
        private static void CheckKeepOut(Result result, in FieldSettings settings, SectorRegionSet regions, in SectorGrid grid, uint seed,
            CellCoord centre, List<FieldRock> rocks)
        {
            var large = new ListRocks(new List<FieldRock>(), int.MaxValue);
            for (int z = -1; z <= 1; ++z)
            {
                for (int y = -1; y <= 1; ++y)
                {
                    for (int x = -1; x <= 1; ++x)
                        FieldGeneration.LargeRocks(settings, regions, grid, seed, new CellCoord(centre.X + x, centre.Y + y, centre.Z + z), ref large);
                }
            }

            float keepOut = settings.KeepOut * (1f + settings.Stretch);
            int inside = 0;
            int smallCount = 0;
            foreach (FieldRock rock in rocks)
            {
                if (rock.Radius * 2f >= settings.LargeDiameter)
                    continue;

                ++smallCount;
                foreach (FieldRock giant in large.Rocks)
                {
                    float reach = giant.Radius * keepOut + rock.Radius;
                    if (math.distancesq(rock.Position, giant.Position) < reach * reach)
                        ++inside;
                }
            }

            result.Check(inside == 0, "Field: small rocks keep out of large ones",
                $"{smallCount} small rocks, {large.Rocks.Count} large rocks around them, {inside} inside one");
        }

        private static void CheckSizes(Result result, in FieldSettings settings, SectorRegionSet regions, in SectorGrid grid, uint seed,
            CellCoord centre)
        {
            int smallCount = 0;
            int overDouble = 0;
            int largeCount = 0;
            bool inRange = true;
            for (int z = -1; z <= 1; ++z)
            {
                for (int y = -1; y <= 1; ++y)
                {
                    for (int x = -1; x <= 1; ++x)
                    {
                        foreach (FieldRock rock in Generate(settings, regions, grid, seed, new CellCoord(centre.X + x, centre.Y + y, centre.Z + z)))
                        {
                            float diameter = rock.Radius * 2f;
                            inRange &= diameter >= settings.MinDiameter * 0.999f && diameter <= settings.MaxDiameter * 1.001f;
                            if (diameter >= settings.LargeDiameter)
                            {
                                ++largeCount;
                                continue;
                            }

                            ++smallCount;
                            if (diameter >= settings.MinDiameter * 2f)
                                ++overDouble;
                        }
                    }
                }
            }

            float share = smallCount > 0 ? (float)overDouble / smallCount : 0f;
            float expectedShare = FieldSettings.Share(settings.MinDiameter * 2f, settings.LargeDiameter, settings.MinDiameter, settings.LargeDiameter,
                settings.SizeExponent);
            float expectedLarge = 27f * settings.LargeCandidates * settings.LargeChance;
            result.Check(inRange && Within(share, expectedShare, 0.15f) && Within(largeCount, expectedLarge, 0.5f), "Field: sizes follow the spread",
                $"{share:P1} of small rocks over twice the smallest ({expectedShare:P1} expected); {largeCount} large rocks in 27 cells " +
                $"(about {expectedLarge:0} expected)");
        }

        private static void CheckRockShapes(Result result)
        {
            var random = new SectorRandom(5u);
            float lowest = float.MaxValue;
            float highest = 0f;
            float leastFacing = 1f;
            float3 offset = FieldRockShape.ShapeOffset(99u, 3);
            for (int i = 0; i < 20000; ++i)
            {
                float z = random.NextFloat() * 2f - 1f;
                float angle = random.NextFloat() * math.PI * 2f;
                float r = math.sqrt(1f - z * z);
                var direction = new float3(r * math.cos(angle), r * math.sin(angle), z);
                float radius = FieldRockShape.Radius(direction, offset, out float3 normal);
                lowest = math.min(lowest, radius);
                highest = math.max(highest, radius);
                leastFacing = math.min(leastFacing, math.dot(normal, direction));
            }

            result.Check(lowest >= FieldRockShape.MinRadius && highest <= FieldRockShape.MaxRadius && leastFacing > 0.2f, "Rock shapes stay in bounds",
                $"radius {lowest:0.00} to {highest:0.00} (bounds {FieldRockShape.MinRadius:0.00} to {FieldRockShape.MaxRadius:0.00}), " +
                $"normals face out by at least {leastFacing:0.00}");

            FieldSettings settings = FieldSettings.Create(1000f, 4f, 1200f, 100f, 2f, 0.3f, 1.3f, 8, 4f, 1000f);
            var rock = new FieldRock(new float3(10f, 20f, 30f), 5f, 12345u);
            float4x4 matrix = FieldRockShape.Matrix(rock, settings, 0f, float3.zero);
            float4x4 later = FieldRockShape.Matrix(rock, settings, 1f, float3.zero);
            float3 scale = new float3(math.length(matrix.c0.xyz), math.length(matrix.c1.xyz), math.length(matrix.c2.xyz));
            bool scaled = math.all(scale >= rock.Radius * (1f - settings.Stretch) - 1e-3f) && math.all(scale <= rock.Radius * (1f + settings.Stretch) + 1e-3f);
            bool placed = math.all(math.abs(matrix.c3.xyz - rock.Position) < 1e-4f) && math.determinant(new float3x3(matrix)) > 0f;
            bool tumbles = !matrix.c0.Equals(later.c0) && math.all(math.abs(later.c3.xyz - rock.Position) < 1e-4f);
            int shape = FieldRockShape.ShapeOf(rock.Seed, settings.Shapes);
            result.Check(scaled && placed && tumbles && shape >= 0 && shape < settings.Shapes, "Rock pose",
                $"stretch within {settings.Stretch:P0}, placed at its centre, turns over time on the spot, shape {shape}");

            // The culling job's size for detail must be the mean scale the shader reads from the matrix, or the morph
            // between levels would not start from nothing.
            float worst = 0f;
            for (uint s = 1; s <= 200; ++s)
            {
                var sample = new FieldRock(float3.zero, 3f + s, s * 2654435761u);
                float4x4 drawn = FieldRockShape.Matrix(sample, settings, s * 0.37f, float3.zero);
                float meanScale = (math.length(drawn.c0.xyz) + math.length(drawn.c1.xyz) + math.length(drawn.c2.xyz)) / 3f;
                worst = math.max(worst, math.abs(meanScale - FieldRockShape.DetailSize(sample, settings.Stretch)) / sample.Radius);
            }

            float4x4 grown = FieldRockShape.Matrix(rock, settings, 0f, float3.zero, 0.5f);
            bool shrinks = math.abs(math.length(grown.c0.xyz) * 2f - math.length(matrix.c0.xyz)) < 1e-3f;
            result.Check(worst < 1e-4f && shrinks, "Rock detail size matches the shader",
                $"largest difference {worst:0.000000} of the radius over 200 rocks, tumbling; half grown is half size");
        }

        // Every level must be closed (each edge shared by two triangles, once each way) and face outwards, or the ink
        // outline, drawn from back faces, breaks.
        private static void CheckIcosphere(Result result)
        {
            bool closed = true;
            bool outward = true;
            bool counted = true;
            var edges = new Dictionary<long, int>();
            float3[] directions = Icosphere.Directions;
            for (int level = 0; level < FieldRockShape.Levels; ++level)
            {
                ushort[] triangles = Icosphere.Indices(level);
                int vertices = Icosphere.VertexCount(level);
                counted &= vertices == 10 * (1 << (2 * level)) + 2 && triangles.Length == 60 * (1 << (2 * level));
                edges.Clear();
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    float3 a = directions[triangles[t]];
                    float3 b = directions[triangles[t + 1]];
                    float3 c = directions[triangles[t + 2]];
                    outward &= math.dot(math.cross(b - a, c - a), a + b + c) > 0f;
                    for (int e = 0; e < 3; ++e)
                    {
                        int from = triangles[t + e];
                        int to = triangles[t + (e + 1) % 3];
                        long key = ((long)math.min(from, to) << 32) | (uint)math.max(from, to);
                        edges.TryGetValue(key, out int balance);
                        edges[key] = balance + (from < to ? 1 : -1);
                    }
                }

                foreach (int balance in edges.Values)
                    closed &= balance == 0;
            }

            result.Check(closed && outward && counted, "Rock meshes are closed and face out",
                $"{FieldRockShape.Levels} levels, {Icosphere.VertexCount(FieldRockShape.Levels - 1):N0} vertices at the finest");

            // Each vertex a level adds must split an edge of the level before, so the morph starts from exactly where
            // that level draws the point.
            int2[] parents = Icosphere.Parents;
            bool split = true;
            for (int level = 1; level < FieldRockShape.Levels; ++level)
            {
                int kept = Icosphere.VertexCount(level - 1);
                for (int v = kept; v < Icosphere.VertexCount(level); ++v)
                {
                    int2 edge = parents[v];
                    split &= edge.x >= 0 && edge.x < kept && edge.y >= 0 && edge.y < kept &&
                             math.lengthsq(directions[v] - math.normalize(directions[edge.x] + directions[edge.y])) < 1e-10f;
                }
            }

            result.Check(split, "Rock detail morphs from the level before", "every added vertex splits an edge of the level before it");
        }

        // A field covering the test grid with a hard edge.
        private static SectorRegion Field(SectorRegionKind kind, float density)
        {
            return new SectorRegion("Field", kind, Vector3.zero, new Vector3(20000f, 20000f, 20000f), density, 0f);
        }

        private static List<FieldRock> Generate<TRegions>(in FieldSettings settings, TRegions regions, in SectorGrid grid, uint seed, CellCoord cell)
            where TRegions : struct, IRegionSet
        {
            var rocks = new ListRocks(new List<FieldRock>(), settings.CellCapacity);
            var nearby = new ListRocks(new List<FieldRock>(), FieldGeneration.NearbyCapacity(settings, grid.CellSize));
            FieldGeneration.Cell(settings, regions, grid, seed, cell, ref rocks, ref nearby);
            return rocks.Rocks;
        }

        private static bool Within(float value, float expected, float tolerance)
        {
            return Math.Abs(value - expected) <= Math.Abs(expected) * tolerance;
        }

        // A list for generation to write into, as the jobs write into native memory.
        private struct ListRocks : IRockBuffer
        {
            public readonly List<FieldRock> Rocks;
            private readonly int limit;

            public ListRocks(List<FieldRock> rocks, int limit)
            {
                Rocks = rocks;
                this.limit = limit;
            }

            public int Count => Rocks.Count;

            public FieldRock Get(int index)
            {
                return Rocks[index];
            }

            public bool Add(in FieldRock rock)
            {
                if (Rocks.Count >= limit)
                    return false;

                Rocks.Add(rock);
                return true;
            }
        }

        // Editor timings, without Burst. A guide to relative cost, not the shipped speed.
        private static void MeasureSpeed(Result result)
        {
            const int iterations = 2000000;

            var random = new SectorRandom(1u);
            float sink = 0f;
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; ++i)
                sink += random.NextFloat();
            double randomNs = watch.Elapsed.TotalMilliseconds * 1000000d / iterations;

            uint hashSink = 0u;
            watch.Restart();
            for (int i = 0; i < iterations; ++i)
                hashSink ^= SectorHash.Cell(99u, new CellCoord(i, i >> 3, -i));
            double hashNs = watch.Elapsed.TotalMilliseconds * 1000000d / iterations;

            var region = new SectorRegion("Speed", SectorRegionKind.AsteroidField, Vector3.zero, new Vector3(900f, 300f, 600f), 1f, 0.5f);
            var point = new Vector3(120f, 40f, -200f);
            watch.Restart();
            for (int i = 0; i < iterations; ++i)
                sink += region.StrengthAt(point);
            double regionNs = watch.Elapsed.TotalMilliseconds * 1000000d / iterations;

            result.Note("Speed", $"random value {randomNs:0.0} ns, cell seed {hashNs:0.0} ns, region strength {regionNs:0.0} ns" +
                (sink + hashSink < 0f ? " " : string.Empty));
        }

        private static int CountBits(uint value)
        {
            int count = 0;
            while (value != 0u)
            {
                value &= value - 1u;
                ++count;
            }

            return count;
        }
    }
}
