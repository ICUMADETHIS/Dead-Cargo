using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// One kind of ore in an asteroid: thin sheets of material where a noise field crosses zero, so it reads as veins
    /// running through the rock. Only found deeper than its minimum depth.
    /// </summary>
    public struct OreLayer
    {
        /// <summary>The material slot the ore writes.</summary>
        public byte Material;

        /// <summary>Noise frequency, per metre. Higher values give more, closer veins.</summary>
        public float Frequency;

        /// <summary>Share of the noise range counted as ore, 0 to 1. Higher values give thicker veins.</summary>
        public float Thickness;

        /// <summary>Depth below the surface where the ore starts, in metres.</summary>
        public float MinDepth;

        /// <summary>Offset into the noise field, from the asteroid's seed.</summary>
        public float3 Offset;

        public bool Contains(float3 position, float depth)
        {
            return depth >= MinDepth && math.abs(noise.snoise(position * Frequency + Offset)) < Thickness;
        }
    }
}
