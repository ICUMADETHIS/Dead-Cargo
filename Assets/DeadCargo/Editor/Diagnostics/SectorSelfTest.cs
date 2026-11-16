using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using DeadCargo.Sectors;
using UnityEngine;

namespace DeadCargo.EditorTools.Diagnostics
{
    /// <summary>
    /// Checks the maths that sector generation is built on: hashing, the random number generator, the cell grid
    /// and region falloff. Pure checks with no scene or editor access, so they run anywhere.
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
            MeasureSpeed(result);
            return result;
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
