using Unity.Mathematics;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// How voxel density is stored: one signed byte per sample holding the distance to the rock surface in voxels,
    /// positive inside, clamped to a narrow band either side of it. The surface lies where the value crosses zero.
    /// </summary>
    public static class VoxelEncoding
    {
        public const sbyte Full = 127;
        public const sbyte Empty = -127;

        /// <summary>Half-width of the stored distance band, in voxels. Distances beyond it saturate.</summary>
        public const float BandVoxels = 2f;

        /// <summary>Material slots a body can hold. Slot 0 is plain rock.</summary>
        public const int MaterialSlots = 8;

        private const float Scale = 127f / BandVoxels;

        /// <summary>Encodes a distance to the surface in voxels, positive inside the rock.</summary>
        public static sbyte Encode(float solidDistanceVoxels)
        {
            return (sbyte)math.round(math.clamp(solidDistanceVoxels * Scale, -127f, 127f));
        }

        /// <summary>The stored distance to the surface in voxels, positive inside the rock.</summary>
        public static float Decode(sbyte value)
        {
            return value / Scale;
        }

        /// <summary>The share of a voxel's cube that is rock, from 0 to 1.</summary>
        public static float Occupancy(sbyte value)
        {
            return math.saturate(value / Scale + 0.5f);
        }

        public static bool IsSolid(sbyte value)
        {
            return value > 0;
        }
    }
}
