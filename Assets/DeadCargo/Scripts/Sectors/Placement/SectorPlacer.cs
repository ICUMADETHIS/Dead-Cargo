using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace DeadCargo.Sectors.Placement
{
    /// <summary>
    /// Picks a spot for one piece of content from the run seed and its rule. Each piece draws from its own stream and
    /// tries candidates in turn, keeping the first that meets every part of the rule; when none does, the closest miss
    /// is used, so content is never lost. Candidates are spread evenly over the ring the rule allows round the arrival
    /// point, the sector's origin. Pure, so the self-test checks it without a scene.
    /// </summary>
    public static class SectorPlacer
    {
        /// <summary>Candidates tried for each piece before the closest miss is used.</summary>
        public const int Attempts = 96;

        /// <summary>Content keeps this far inside the sector's bounds, in metres.</summary>
        public const float EdgeMargin = 200f;

        // Separates placement from every other use of the sector seed.
        private const uint PlacementStream = 0x91ACE001u;

        // A spot outside the sector is a far worse miss than one in the wrong density or a little crowded.
        private const float OutsidePerMetre = 0.01f;

        /// <summary>
        /// Finds a spot for piece <paramref name="index"/>, in sector space, and its heading in degrees. Returns false
        /// when no candidate met the rule and the closest miss was used. <paramref name="taken"/> holds the spots of
        /// content placed before it.
        /// </summary>
        public static bool TryPlace<TRegions>(in TRegions regions, in SectorGrid grid, uint sectorSeed, int index,
            in PlacementSettings settings, IReadOnlyList<Vector3> taken, out Vector3 position, out float heading)
            where TRegions : struct, IRegionSet
        {
            var random = new SectorRandom(SpotSeed(sectorSeed, index));
            float innerSquared = settings.MinDistance * settings.MinDistance;
            float outerSquared = settings.MaxDistance * settings.MaxDistance;

            float closest = float.MaxValue;
            position = Vector3.zero;
            heading = 0f;

            for (int attempt = 0; attempt < Attempts; ++attempt)
            {
                // Every candidate draws the same values, so one candidate never shifts the next.
                float angle = random.Range(0f, 2f * math.PI);
                float distance = math.sqrt(math.lerp(innerSquared, outerSquared, random.NextFloat()));
                float height = random.Range(-settings.MaxHeight, settings.MaxHeight);
                float turn = random.Range(-180f, 180f);

                var candidate = new Vector3(math.sin(angle) * distance, height, math.cos(angle) * distance);
                float miss = Miss(regions, grid, settings, taken, candidate);
                if (miss >= closest)
                    continue;

                closest = miss;
                position = candidate;
                heading = settings.FaceArrival ? HeadingToArrival(candidate) : turn;
                if (miss <= 0f)
                    return true;
            }

            return false;
        }

        /// <summary>How far a spot falls short of its rule: 0 when it meets every part, more the further it misses.</summary>
        public static float Miss<TRegions>(in TRegions regions, in SectorGrid grid, in PlacementSettings settings,
            IReadOnlyList<Vector3> taken, Vector3 position)
            where TRegions : struct, IRegionSet
        {
            float3 low = (float3)grid.Min + EdgeMargin;
            float3 high = (float3)(grid.Min + grid.Size) - EdgeMargin;
            float3 point = position;
            float miss = math.csum(math.max(low - point, 0f) + math.max(point - high, 0f)) * OutsidePerMetre;

            float density = SectorDensity.DensityAt(regions, point, out _);
            miss += math.max(settings.MinDensity - density, 0f) + math.max(density - settings.MaxDensity, 0f);

            if (settings.Spacing > 0f && taken != null)
            {
                float nearest = float.MaxValue;
                for (int i = 0; i < taken.Count; ++i)
                    nearest = math.min(nearest, Vector3.Distance(taken[i], position));

                miss += math.max(settings.Spacing - nearest, 0f) / settings.Spacing;
            }

            return miss;
        }

        /// <summary>The heading, in degrees, that faces a spot back towards the arrival point across the horizontal plane.</summary>
        public static float HeadingToArrival(Vector3 position)
        {
            return position.x * position.x + position.z * position.z > 1e-4f
                ? math.degrees(math.atan2(-position.x, -position.z))
                : 0f;
        }

        /// <summary>The seed one piece of content draws its candidates from.</summary>
        public static uint SpotSeed(uint sectorSeed, int index)
        {
            return SectorHash.Combine(SectorHash.Combine(sectorSeed, PlacementStream), unchecked((uint)index));
        }
    }
}
