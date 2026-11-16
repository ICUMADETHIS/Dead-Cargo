using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// The shape and ore of a procedural asteroid, as a distance field. An ellipsoid is lifted by rolling noise and
    /// pitted with craters, then roughened with fine detail. A pure function of its settings, so the same seed always
    /// builds the same rock. Positions are body-local, in metres.
    /// </summary>
    public struct AsteroidShape
    {
        public const int MaxOreLayers = 4;

        // How high a crater's rim stands, as a share of its depth.
        private const float RimShare = 0.3f;
        private const float RimSharpness = 12f;

        /// <summary>Radii of the base ellipsoid, in metres.</summary>
        public float3 Radii;

        /// <summary>Height of the rolling relief, as a share of the radius.</summary>
        public float Bumpiness;

        /// <summary>Relief features around the rock. Higher values give more, smaller lumps.</summary>
        public float BumpFrequency;

        public int BumpOctaves;

        /// <summary>Crater depth, as a share of the radius. 0 for none.</summary>
        public float CraterDepth;

        /// <summary>Craters around the rock. Higher values give more, smaller craters.</summary>
        public float CraterFrequency;

        /// <summary>How much of its cell each crater covers, 0 to 1.</summary>
        public float CraterSize;

        /// <summary>Height of the fine surface detail, in metres.</summary>
        public float Detail;

        /// <summary>Fine detail frequency, per metre.</summary>
        public float DetailFrequency;

        /// <summary>Offset into every noise field, from the seed.</summary>
        public float3 Offset;

        public OreLayer Ore0;
        public OreLayer Ore1;
        public OreLayer Ore2;
        public OreLayer Ore3;
        public int OreCount;

        /// <summary>No part of the rock reaches further from the centre than this, in metres.</summary>
        public float MaxRadius => math.cmax(Radii) * (1f + Bumpiness + CraterDepth * RimShare) + Detail;

        /// <summary>Every point closer to the centre than this is rock, in metres.</summary>
        public float MinRadius => math.max(0f, math.cmin(Radii) * (1f - Bumpiness - CraterDepth) - Detail);

        /// <summary>Distance to the surface along the line from the centre, in metres, positive inside the rock.</summary>
        public float SolidDistance(float3 position)
        {
            float distance = math.length(position);
            if (distance < 1e-4f)
                return math.cmin(Radii);

            float3 direction = position / distance;
            float ellipsoidRadius = 1f / math.length(direction / Radii);
            float relief = Bumpiness * Fbm(direction * BumpFrequency + Offset, BumpOctaves) - Crater(direction);
            float detail = Detail * noise.snoise(position * DetailFrequency + Offset * 1.31f);
            return ellipsoidRadius * (1f + relief) + detail - distance;
        }

        /// <summary>The material slot at a position the given depth below the surface, in metres.</summary>
        public byte MaterialAt(float3 position, float depth)
        {
            for (int i = 0; i < OreCount; ++i)
            {
                OreLayer layer = GetOre(i);
                if (layer.Contains(position, depth))
                    return layer.Material;
            }

            return 0;
        }

        public OreLayer GetOre(int index)
        {
            switch (index)
            {
                case 0: return Ore0;
                case 1: return Ore1;
                case 2: return Ore2;
                default: return Ore3;
            }
        }

        public void SetOre(int index, OreLayer layer)
        {
            switch (index)
            {
                case 0: Ore0 = layer; break;
                case 1: Ore1 = layer; break;
                case 2: Ore2 = layer; break;
                default: Ore3 = layer; break;
            }
        }

        // A bowl with a raised rim, centred on each cell of a Voronoi pattern over the sphere of directions.
        private float Crater(float3 direction)
        {
            if (CraterDepth <= 0f)
                return 0f;

            float2 cell = noise.cellular(direction * CraterFrequency + Offset * 0.77f);
            float d = cell.x / math.max(CraterSize, 1e-3f);
            float bowl = math.saturate(1f - d * d);
            float rim = math.exp(-(d - 1f) * (d - 1f) * RimSharpness) * RimShare;
            return CraterDepth * (bowl * bowl - rim);
        }

        private static float Fbm(float3 p, int octaves)
        {
            float sum = 0f;
            float amplitude = 1f;
            float total = 0f;
            for (int i = 0; i < octaves; ++i)
            {
                sum += noise.snoise(p) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                p *= 2.03f;
            }

            return total > 0f ? sum / total : 0f;
        }
    }
}
