using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DeadCargo.Sectors.Field
{
    /// <summary>A rock near enough to be solid.</summary>
    public struct SolidCandidate
    {
        /// <summary>The rock's place in the field's rock array.</summary>
        public int Rock;

        /// <summary>From the focus to the rock's surface, in metres. Negative inside its reach.</summary>
        public float Distance;

        /// <summary>
        /// True within the solid range itself; false in the margin beyond it, where a rock that is solid stays so but
        /// none turns solid, so a rock on the edge never flickers.
        /// </summary>
        public bool InRange;
    }

    /// <summary>Nearest surface first.</summary>
    public struct NearestSolidFirst : IFieldOrder<SolidCandidate>
    {
        public bool Before(in SolidCandidate a, in SolidCandidate b)
        {
            return a.Distance < b.Distance || (a.Distance == b.Distance && a.Rock < b.Rock);
        }
    }

    /// <summary>
    /// Finds the rocks near enough to the focus to be solid, nearest first. A rock is solid while its surface is
    /// within <see cref="Range"/> plus <see cref="RangePerRadius"/> for every metre of its radius, so a boulder is
    /// solid close by and a giant from kilometres off. Rocks come largest first in each cell, so a cell is left at the
    /// first rock too small to be solid from the cell's nearest point: far cells cost one test each.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast)]
    public struct FieldSolidJob : IJob
    {
        // Half the diagonal of a unit cube: a cell's furthest corner from its centre, per metre of its edge.
        private const float HalfDiagonal = 0.8660254f;

        [ReadOnly] public NativeArray<FieldRock> Rocks;
        [ReadOnly] public NativeArray<int> Counts;

        /// <summary>The lowest corner of each slot's cell, in sector space.</summary>
        [ReadOnly] public NativeArray<float3> SlotMins;

        public int Slots;
        public int Capacity;
        public float CellSize;

        /// <summary>What the rocks are solid for, in sector space: the player's vehicle.</summary>
        public float3 Focus;

        /// <summary>Furthest a rock's surface reaches, as a multiple of its radius.</summary>
        public float Reach;

        public float Range;
        public float RangePerRadius;

        /// <summary>How far past its range a solid rock stays solid, in metres.</summary>
        public float KeepMargin;

        /// <summary>The rocks found, nearest first; when more are found than fit, the nearest are kept.</summary>
        public NativeArray<SolidCandidate> Candidates;

        /// <summary>One entry: how many candidates were written.</summary>
        [WriteOnly] public NativeArray<int> Found;

        public void Execute()
        {
            int found = 0;
            float cellRadius = CellSize * HalfDiagonal;
            for (int slot = 0; slot < Slots; ++slot)
            {
                int count = Counts[slot];
                if (count == 0)
                    continue;

                int start = slot * Capacity;
                float cellNear = math.max(math.distance(SlotMins[slot] + CellSize * 0.5f, Focus) - cellRadius, 0f);
                for (int i = 0; i < count; ++i)
                {
                    FieldRock rock = Rocks[start + i];
                    float reach = rock.Radius * Reach;
                    float kept = Range + rock.Radius * RangePerRadius + KeepMargin;

                    // Out of range even from the cell's nearest point, and every rock after it is smaller still.
                    if (cellNear - reach >= kept)
                        break;

                    float distance = math.distance(rock.Position, Focus) - reach;
                    if (distance >= kept)
                        continue;

                    var candidate = new SolidCandidate { Rock = start + i, Distance = distance, InRange = distance < kept - KeepMargin };
                    if (found < Candidates.Length)
                        Candidates[found++] = candidate;
                    else
                        ReplaceFurthest(candidate);
                }
            }

            FieldSort.Sort(Candidates, 0, found, new NearestSolidFirst());
            Found[0] = found;
        }

        // A full list keeps the nearest: the newcomer takes the furthest one's place, if it is nearer.
        private void ReplaceFurthest(in SolidCandidate candidate)
        {
            int furthest = 0;
            for (int i = 1; i < Candidates.Length; ++i)
            {
                if (Candidates[i].Distance > Candidates[furthest].Distance)
                    furthest = i;
            }

            if (candidate.Distance < Candidates[furthest].Distance)
                Candidates[furthest] = candidate;
        }
    }
}
