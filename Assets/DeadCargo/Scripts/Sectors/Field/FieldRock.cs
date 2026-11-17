using Unity.Mathematics;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// One rock of an asteroid field: where it is, how big, and the seed its shape, turn and tumble come from. Kept
    /// to 20 bytes, as a loaded field holds hundreds of thousands; the rest is worked out from the seed when drawn.
    /// </summary>
    public struct FieldRock
    {
        /// <summary>Centre in sector space, in metres.</summary>
        public float3 Position;

        /// <summary>Mean radius, in metres.</summary>
        public float Radius;

        public uint Seed;

        public FieldRock(float3 position, float radius, uint seed)
        {
            Position = position;
            Radius = radius;
            Seed = seed;
        }
    }
}
