using UnityEngine;

namespace DeadCargo.Sectors
{
    /// <summary>
    /// A small seeded random number generator for sector generation. Two generators made from the same seed
    /// give the same sequence. A value type with no shared state, so each cell and each job owns its own.
    /// </summary>
    public struct SectorRandom
    {
        private const uint Step = 0x9E3779B9u;
        private const float UnitScale = 1f / 16777216f;

        private uint state;

        public SectorRandom(uint seed)
        {
            state = seed;
        }

        /// <summary>The next 32 random bits.</summary>
        public uint NextUInt()
        {
            unchecked
            {
                state += Step;
            }

            return SectorHash.Mix(state);
        }

        /// <summary>A value from 0 up to, but not including, 1.</summary>
        public float NextFloat()
        {
            // The top 24 bits fill a float's mantissa exactly, so the result never rounds up to 1.
            return (NextUInt() >> 8) * UnitScale;
        }

        /// <summary>A value from <paramref name="min"/> up to, but not including, <paramref name="max"/>.</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>A whole number from <paramref name="min"/> up to, but not including, <paramref name="maxExclusive"/>.</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min)
                return min;

            return min + (int)(NextUInt() % (uint)(maxExclusive - min));
        }

        /// <summary>A point inside a box given by its minimum corner and size.</summary>
        public Vector3 NextInBox(Vector3 min, Vector3 size)
        {
            float x = NextFloat();
            float y = NextFloat();
            float z = NextFloat();
            return new Vector3(min.x + size.x * x, min.y + size.y * y, min.z + size.z * z);
        }
    }
}
