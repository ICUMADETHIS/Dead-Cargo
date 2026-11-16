using System;
using DeadCargo.Sectors;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// The nebula as a function of direction: cloud density, gathered into a band along the galactic plane, and a
    /// low-frequency hue field that picks between the shader's two palettes. Values are raw; the baker normalises them.
    /// </summary>
    public sealed class NebulaField
    {
        /// <summary>Raise whenever this field's maths changes, so bakes made with the old maths are redone.</summary>
        public const int Version = 2;

        private const int SwirlOctaves = 2;
        private const int HueOctaves = 3;
        private const float Lacunarity = 2f;
        private const float Gain = 0.5f;

        // How far the hue field bends the band away from a straight line, in sky-height units.
        private const float BandBend = 0.25f;

        // An orthonormal rotation applied between octaves, so their noise lattices never line up.
        private const float R00 = 0f, R01 = 0.8f, R02 = 0.6f;
        private const float R10 = -0.8f, R11 = 0.36f, R12 = -0.48f;
        private const float R20 = -0.6f, R21 = -0.48f, R22 = 0.64f;

        private readonly NebulaShape shape;
        private readonly NebulaNoise cloudNoise;
        private readonly NebulaNoise swirlNoise;
        private readonly NebulaNoise hueNoise;

        public NebulaField(NebulaShape shape)
        {
            this.shape = shape;
            uint seed = SectorHash.Mix(unchecked((uint)shape.Seed));
            cloudNoise = new NebulaNoise(SectorHash.Combine(seed, 0u));
            swirlNoise = new NebulaNoise(SectorHash.Combine(seed, 1u));
            hueNoise = new NebulaNoise(SectorHash.Combine(seed, 2u));
        }

        /// <summary>Density and hue for a unit direction in sky space, where Y is the galactic pole.</summary>
        public void Sample(float x, float y, float z, out float density, out float hue)
        {
            float px = x * shape.Scale;
            float py = y * shape.Scale;
            float pz = z * shape.Scale;
            if (shape.Swirl > 0f)
            {
                // Domain warping: a second field pushes the sample point around, which twists the clouds.
                float wx = Fractal(swirlNoise, px + 5.2f, py + 1.3f, pz + 2.8f, SwirlOctaves);
                float wy = Fractal(swirlNoise, px + 1.7f, py + 9.2f, pz + 4.1f, SwirlOctaves);
                float wz = Fractal(swirlNoise, px + 8.3f, py + 2.8f, pz + 7.4f, SwirlOctaves);
                px += shape.Swirl * wx;
                py += shape.Swirl * wy;
                pz += shape.Swirl * wz;
            }

            // The hue field is sampled through the same swirl, so palette changes follow the clouds' flow.
            float hueStep = shape.HueScale / shape.Scale;
            hue = Fractal(hueNoise, px * hueStep + 31.7f, py * hueStep + 11.3f, pz * hueStep + 47.1f, HueOctaves);

            float cloud = Fractal(cloudNoise, px, py, pz, shape.Octaves) * 0.5f + 0.5f;
            float offset = (y + hue * BandBend) / shape.BandWidth;
            float band = (float)Math.Exp(-offset * offset);
            density = cloud * (1f - shape.BandFocus + shape.BandFocus * band);
        }

        // Fractal noise: octaves at doubling frequency and halving amplitude, averaged.
        private static float Fractal(NebulaNoise noise, float x, float y, float z, int octaves)
        {
            float sum = 0f;
            float total = 0f;
            float amplitude = 1f;
            for (int i = 0; i < octaves; ++i)
            {
                sum += amplitude * noise.Sample(x, y, z);
                total += amplitude;
                amplitude *= Gain;

                float rx = R00 * x + R01 * y + R02 * z;
                float ry = R10 * x + R11 * y + R12 * z;
                float rz = R20 * x + R21 * y + R22 * z;
                x = rx * Lacunarity;
                y = ry * Lacunarity;
                z = rz * Lacunarity;
            }

            return sum / total;
        }
    }
}
