using System;
using System.Threading.Tasks;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// Bakes the nebula field into texels of an octahedral map, on every core. Plain maths with no Unity calls,
    /// so it can be checked outside the editor. NebulaMap turns the result into a texture.
    /// </summary>
    public static class NebulaBaker
    {
        /// <summary>
        /// Map size in texels, border included. At 1024 a texel is about 0.2 degrees of sky, which keeps cloud
        /// edges smooth, because the shader thresholds the filtered value rather than showing texels directly.
        /// </summary>
        public const int MapSize = 1024;

        public const int Channels = 2;

        /// <summary>
        /// Density and hue for every texel, interleaved, row by row from the bottom. Each channel is stretched to
        /// fill 0..1, so the shader's step thresholds mean the same thing for every shape. <paramref name="maxThreads"/>
        /// limits the cores it takes; -1 takes them all.
        /// </summary>
        public static float[] Bake(NebulaShape shape, int size = MapSize, int maxThreads = -1)
        {
            var field = new NebulaField(shape);
            var texels = new float[size * size * Channels];
            var options = new ParallelOptions { MaxDegreeOfParallelism = maxThreads };

            Parallel.For(0, size, options, row =>
            {
                int index = row * size * Channels;
                for (int column = 0; column < size; ++column, index += Channels)
                {
                    OctahedralMapping.TexelDirection(column, row, size, out float x, out float y, out float z);
                    field.Sample(x, y, z, out float density, out float hue);
                    texels[index] = density;
                    texels[index + 1] = hue;
                }
            });

            for (int channel = 0; channel < Channels; ++channel)
                Stretch(texels, channel);

            return texels;
        }

        private static void Stretch(float[] texels, int channel)
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = channel; i < texels.Length; i += Channels)
            {
                min = Math.Min(min, texels[i]);
                max = Math.Max(max, texels[i]);
            }

            float scale = max > min ? 1f / (max - min) : 0f;
            for (int i = channel; i < texels.Length; i += Channels)
                texels[i] = (texels[i] - min) * scale;
        }
    }
}
