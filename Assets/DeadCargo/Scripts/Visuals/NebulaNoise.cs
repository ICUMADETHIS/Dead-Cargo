using DeadCargo.Sectors;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// Seeded 3D gradient noise (Perlin's improved noise). Smooth everywhere, roughly -1 to 1, and the same for the
    /// same seed on every machine. Read-only once built, so one instance can be sampled from many threads.
    /// </summary>
    public sealed class NebulaNoise
    {
        private const int TableSize = 256;
        private const int TableMask = TableSize - 1;

        // Doubled, so lookups never wrap.
        private readonly byte[] permutation = new byte[TableSize * 2];

        public NebulaNoise(uint seed)
        {
            for (int i = 0; i < TableSize; ++i)
                permutation[i] = (byte)i;

            var random = new SectorRandom(seed);
            for (int i = TableSize - 1; i > 0; --i)
            {
                int j = random.Range(0, i + 1);
                (permutation[i], permutation[j]) = (permutation[j], permutation[i]);
            }

            for (int i = 0; i < TableSize; ++i)
                permutation[TableSize + i] = permutation[i];
        }

        public float Sample(float x, float y, float z)
        {
            int xi = FastFloor(x);
            int yi = FastFloor(y);
            int zi = FastFloor(z);
            float xf = x - xi;
            float yf = y - yi;
            float zf = z - zi;
            xi &= TableMask;
            yi &= TableMask;
            zi &= TableMask;

            float u = Fade(xf);
            float v = Fade(yf);
            float w = Fade(zf);

            byte[] p = permutation;
            int a = p[xi] + yi;
            int aa = p[a] + zi;
            int ab = p[a + 1] + zi;
            int b = p[xi + 1] + yi;
            int ba = p[b] + zi;
            int bb = p[b + 1] + zi;

            float x0 = Lerp(u, Gradient(p[aa], xf, yf, zf), Gradient(p[ba], xf - 1f, yf, zf));
            float x1 = Lerp(u, Gradient(p[ab], xf, yf - 1f, zf), Gradient(p[bb], xf - 1f, yf - 1f, zf));
            float x2 = Lerp(u, Gradient(p[aa + 1], xf, yf, zf - 1f), Gradient(p[ba + 1], xf - 1f, yf, zf - 1f));
            float x3 = Lerp(u, Gradient(p[ab + 1], xf, yf - 1f, zf - 1f), Gradient(p[bb + 1], xf - 1f, yf - 1f, zf - 1f));
            return Lerp(w, Lerp(v, x0, x1), Lerp(v, x2, x3));
        }

        private static int FastFloor(float value)
        {
            int truncated = (int)value;
            return value < truncated ? truncated - 1 : truncated;
        }

        private static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static float Lerp(float t, float a, float b)
        {
            return a + t * (b - a);
        }

        // One of twelve edge directions of a cube, picked by the hash.
        private static float Gradient(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }
}
