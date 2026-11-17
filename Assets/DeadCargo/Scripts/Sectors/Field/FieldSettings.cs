using Unity.Mathematics;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// An asteroid field's generation settings as plain values, with the per-cell counts worked out, so generation
    /// jobs can read them. Built by <see cref="AsteroidFieldDefinition.ToSettings"/>.
    /// </summary>
    public struct FieldSettings
    {
        /// <summary>Smallest, largest and smallest "large" rock diameter, in metres.</summary>
        public float MinDiameter;
        public float MaxDiameter;
        public float LargeDiameter;

        /// <summary>Slope of the size spread: the count of rocks larger than a size falls as that size to this power.</summary>
        public float SizeExponent;

        /// <summary>Points tested per cell, and the chance each is kept at full density, for small and for large rocks.</summary>
        public int SmallCandidates;
        public float SmallChance;
        public int LargeCandidates;
        public float LargeChance;

        /// <summary>How far a rock may stretch along each axis, as a share of its size.</summary>
        public float Stretch;

        /// <summary>Small rocks keep out of a large rock to this many times its radius.</summary>
        public float KeepOut;

        /// <summary>Number of rock shapes.</summary>
        public int Shapes;

        /// <summary>Tumble of a 10 m rock, in radians per second. Larger rocks turn more slowly.</summary>
        public float Tumble;

        /// <summary>
        /// How strongly rocks gather into clumps with gaps between, 0 to 1. 0 spreads them evenly; 1 empties the gaps.
        /// Clumps keep the field's full density, so clumping thins the field overall.
        /// </summary>
        public float Clumping;

        /// <summary>Distance across a clump and the gap beside it, in metres.</summary>
        public float ClumpSize;

        /// <summary>Share of a cell's small rocks that gather as rubble round its large rocks, 0 to 1.</summary>
        public float Rubble;

        /// <summary>Rocks one cell can hold at most: every candidate kept.</summary>
        public int CellCapacity => SmallCandidates + LargeCandidates;

        /// <summary>The largest radius any rock can reach in any direction, shape and stretch included, in metres.</summary>
        public float MaxReach => MaxDiameter * 0.5f * (1f + Stretch) * FieldRockShape.MaxRadius;

        /// <summary>Expected rocks per cell at full density.</summary>
        public float ExpectedPerCell => SmallCandidates * SmallChance + LargeCandidates * LargeChance;

        /// <summary>
        /// Builds the settings for cells of the given size from a density per cubic kilometre and a size spread, which
        /// fix how many rocks of each band a cell expects.
        /// </summary>
        public static FieldSettings Create(float rocksPerCubicKilometre, float minDiameter, float maxDiameter, float largeDiameter,
            float sizeExponent, float stretch, float keepOut, int shapes, float tumbleDegrees, float cellSize, float clumping = 0f,
            float clumpSize = 700f, float rubble = 0f)
        {
            minDiameter = math.max(minDiameter, 0.5f);
            maxDiameter = math.max(maxDiameter, minDiameter * 1.01f);
            largeDiameter = math.clamp(largeDiameter, minDiameter, maxDiameter);
            sizeExponent = math.max(sizeExponent, 0.1f);

            float cellCubicKilometres = cellSize * cellSize * cellSize * 1e-9f;
            float total = math.max(rocksPerCubicKilometre, 0f) * cellCubicKilometres;
            float largeShare = Share(largeDiameter, maxDiameter, minDiameter, maxDiameter, sizeExponent);
            float small = total * (1f - largeShare);
            float large = total * largeShare;

            // Large rocks are rare, so a few more candidates than expected keep their count close to the target.
            int largeCandidates = math.max(4, (int)math.ceil(large * 3f));
            int smallCandidates = math.max(1, (int)math.ceil(small));

            return new FieldSettings
            {
                MinDiameter = minDiameter,
                MaxDiameter = maxDiameter,
                LargeDiameter = largeDiameter,
                SizeExponent = sizeExponent,
                SmallCandidates = smallCandidates,
                SmallChance = math.saturate(small / smallCandidates),
                LargeCandidates = largeCandidates,
                LargeChance = math.saturate(large / largeCandidates),
                Stretch = math.clamp(stretch, 0f, 0.6f),
                KeepOut = math.max(keepOut, 0f),
                Shapes = math.clamp(shapes, 1, FieldRockShape.MaxShapes),
                Tumble = math.radians(math.max(tumbleDegrees, 0f)),
                Clumping = math.saturate(clumping),
                ClumpSize = math.max(clumpSize, 50f),
                Rubble = math.saturate(rubble)
            };
        }

        /// <summary>Share of rocks from <paramref name="from"/> to <paramref name="to"/> across the whole spread.</summary>
        public static float Share(float from, float to, float min, float max, float exponent)
        {
            float all = math.pow(min, -exponent) - math.pow(max, -exponent);
            return all > 0f ? math.saturate((math.pow(from, -exponent) - math.pow(to, -exponent)) / all) : 0f;
        }
    }
}
