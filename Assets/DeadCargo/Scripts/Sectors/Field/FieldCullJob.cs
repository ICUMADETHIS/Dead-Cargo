using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// Decides, for every loaded rock, whether it is drawn this frame and in which batch: one cell slot per index. A
    /// rock is drawn when it is in view, within draw distance and at least a pixel or two across. Its batch is its
    /// shape, its level of detail (from its size on screen) and whether it casts shadows (from its distance). Writes
    /// each rock's batch, or <see cref="Culled"/>, and counts each batch per slot for <see cref="FieldOffsetJob"/>.
    /// <para>
    /// A cell wholly out of view or range is skipped at once. Within a cell, rocks come largest first, so the loop
    /// stops at the first rock too small to draw from the cell's nearest point: a far cell costs a few giants, not
    /// a thousand tests. <see cref="Examined"/> says how far each cell got.
    /// </para>
    /// Size on screen is measured exactly as the comic surface's detail morph measures it, so the morph starts from
    /// nothing at the moment a rock changes level.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast)]
    public struct FieldCullJob : IJobParallelFor
    {
        public const byte Culled = byte.MaxValue;

        // A level of detail lasts until a rock is twice as wide on screen: from 20 triangles below this many pixels
        // across, so edges stay near 8 pixels at every level.
        public const float FirstDetailPixels = 12f;

        // Half the diagonal of a unit cube: a cell's furthest corner from its centre, per metre of its edge.
        private const float HalfDiagonal = 0.8660254f;

        [ReadOnly] public NativeArray<FieldRock> Rocks;
        [ReadOnly] public NativeArray<int> Counts;

        /// <summary>The lowest corner of each slot's cell, in sector space.</summary>
        [ReadOnly] public NativeArray<float3> SlotMins;

        /// <summary>The camera's left, right, bottom and top planes in sector space: normal inwards, then offset.</summary>
        [ReadOnly] public NativeArray<float4> Planes;

        public int Capacity;
        public int Batches;
        public int Shapes;
        public float CellSize;
        public float3 Eye;

        /// <summary>From the camera to where it will be shortly; see <see cref="FieldRockShape.DetailDistance"/>.</summary>
        public float3 Ahead;

        public float DrawDistance;
        public float ShadowDistance;

        /// <summary>Screen pixels per radian of view, to turn size over distance into pixels.</summary>
        public float PixelsPerRadian;
        public float MinPixels;
        public float DetailBias;

        /// <summary>Furthest a rock's surface reaches, as a multiple of its radius.</summary>
        public float Reach;

        /// <summary>The field's stretch, for each rock's detail size.</summary>
        public float Stretch;

        /// <summary>Slack on the view planes per metre of distance, so a camera that turns this frame loses no rock.</summary>
        public float Slack;

        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<byte> RockBatches;
        [NativeDisableParallelForRestriction] public NativeArray<int> BatchCounts;

        /// <summary>Per slot: how many of its rocks, from the first, were given a batch or culled. The rest are not drawn.</summary>
        [WriteOnly] public NativeArray<int> Examined;

        public void Execute(int slot)
        {
            int row = slot * Batches;
            for (int b = 0; b < Batches; ++b)
                BatchCounts[row + b] = 0;

            Examined[slot] = 0;
            int count = Counts[slot];
            if (count == 0)
                return;

            // The whole cell at once: a ball round it, grown by the reach of its largest rock, which comes first.
            int start = slot * Capacity;
            float3 centre = SlotMins[slot] + CellSize * 0.5f;
            float cellRadius = CellSize * HalfDiagonal;
            float cellReach = cellRadius + Rocks[start].Radius * Reach;
            float cellDistance = math.distance(centre, Eye);
            if (cellDistance - cellReach > DrawDistance || !InView(centre, cellReach + (cellDistance + cellRadius) * Slack))
                return;

            float nearestDetail = math.max(FieldRockShape.DetailDistance(centre, Eye, Ahead) - cellRadius, 0f);
            float largestStretch = 1f + Stretch;
            int examined = 0;
            for (; examined < count; ++examined)
            {
                int index = start + examined;
                FieldRock rock = Rocks[index];

                // Too small to draw even from the cell's nearest point, and every rock after it is smaller still.
                if (FieldRockShape.Pixels(rock.Radius * largestStretch, nearestDetail, PixelsPerRadian) < MinPixels)
                    break;

                RockBatches[index] = Culled;

                float distance = math.distance(rock.Position, Eye);
                float reach = rock.Radius * Reach;
                float near = distance - reach;
                if (near > DrawDistance || !InView(rock.Position, reach + distance * Slack))
                    continue;

                float detailDistance = FieldRockShape.DetailDistance(rock.Position, Eye, Ahead);
                float pixels = FieldRockShape.Pixels(FieldRockShape.DetailSize(rock, Stretch), detailDistance, PixelsPerRadian);
                if (pixels < MinPixels)
                    continue;

                int detail = Detail(pixels * DetailBias);
                int shape = FieldRockShape.ShapeOf(rock.Seed, Shapes);
                int shadow = near < ShadowDistance ? 1 : 0;
                int batch = (detail * Shapes + shape) * 2 + shadow;
                RockBatches[index] = (byte)batch;
                BatchCounts[row + batch]++;
            }

            Examined[slot] = examined;
        }

        private bool InView(float3 position, float margin)
        {
            for (int p = 0; p < 4; ++p)
            {
                float4 plane = Planes[p];
                if (math.dot(plane.xyz, position) + plane.w < -margin)
                    return false;
            }

            return true;
        }

        private static int Detail(float pixels)
        {
            int detail = 0;
            float threshold = FirstDetailPixels;
            while (detail < FieldRockShape.Levels - 1 && pixels >= threshold)
            {
                ++detail;
                threshold *= 2f;
            }

            return detail;
        }
    }
}
