namespace DeadCargo.Sectors
{
    /// <summary>
    /// Integer hashing for sector generation. The same inputs always give the same output, on every machine,
    /// which is what lets a sector be rebuilt from its seed alone. Plain value maths, so it runs inside jobs.
    /// </summary>
    public static class SectorHash
    {
        private const uint GoldenRatio = 0x9E3779B9u;

        /// <summary>Scrambles a value so that every input bit affects every output bit. The MurmurHash3 finaliser.</summary>
        public static uint Mix(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x85EBCA6Bu;
                value ^= value >> 13;
                value *= 0xC2B2AE35u;
                value ^= value >> 16;
                return value;
            }
        }

        /// <summary>Folds another value into a hash. Order matters: (a, b) and (b, a) give different results.</summary>
        public static uint Combine(uint hash, uint value)
        {
            unchecked
            {
                return Mix(hash ^ (value + GoldenRatio + (hash << 6) + (hash >> 2)));
            }
        }

        /// <summary>
        /// The seed for one cell of a sector. <paramref name="stream"/> separates independent uses of the same cell,
        /// such as asteroids and loot, so adding one never shifts the other.
        /// </summary>
        public static uint Cell(uint sectorSeed, CellCoord cell, uint stream = 0u)
        {
            unchecked
            {
                uint hash = Mix(sectorSeed);
                hash = Combine(hash, (uint)cell.X);
                hash = Combine(hash, (uint)cell.Y);
                hash = Combine(hash, (uint)cell.Z);
                return Combine(hash, stream);
            }
        }

        /// <summary>A stable hash of text, for turning a name into a seed. Unlike string.GetHashCode, it never changes between runs.</summary>
        public static uint Text(string text)
        {
            unchecked
            {
                // FNV-1a.
                uint hash = 2166136261u;
                if (text != null)
                {
                    for (int i = 0; i < text.Length; ++i)
                        hash = (hash ^ text[i]) * 16777619u;
                }

                return Mix(hash);
            }
        }
    }
}
