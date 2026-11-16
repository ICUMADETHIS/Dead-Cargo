using System;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// Maps directions to a square texture and back, with Y as the pole: the upper half of the sky fills the inner
    /// diamond and the lower half folds into the corners. Maps carry a one-texel border holding the far side of each
    /// fold, so bilinear filtering is seamless everywhere. Must match OctahedralEncode in SpaceSky.shader.
    /// </summary>
    public static class OctahedralMapping
    {
        /// <summary>Map coordinates in -1..1 for a unit direction.</summary>
        public static void Encode(float x, float y, float z, out float u, out float v)
        {
            float sum = Math.Abs(x) + Math.Abs(y) + Math.Abs(z);
            u = x / sum;
            v = z / sum;
            if (y < 0f)
            {
                float folded = (1f - Math.Abs(v)) * (u >= 0f ? 1f : -1f);
                v = (1f - Math.Abs(u)) * (v >= 0f ? 1f : -1f);
                u = folded;
            }
        }

        /// <summary>The unit direction for map coordinates in -1..1.</summary>
        public static void Decode(float u, float v, out float x, out float y, out float z)
        {
            y = 1f - Math.Abs(u) - Math.Abs(v);
            if (y < 0f)
            {
                float unfolded = (1f - Math.Abs(v)) * (u >= 0f ? 1f : -1f);
                v = (1f - Math.Abs(u)) * (v >= 0f ? 1f : -1f);
                u = unfolded;
            }

            float length = (float)Math.Sqrt(u * u + y * y + v * v);
            x = u / length;
            y /= length;
            z = v / length;
        }

        /// <summary>
        /// Brings coordinates just outside -1..1 back to the point they continue into. Crossing an outer edge of the
        /// map continues on the same edge, mirrored about its midpoint.
        /// </summary>
        public static void Fold(ref float u, ref float v)
        {
            if (u > 1f)
            {
                u = 2f - u;
                v = -v;
            }
            else if (u < -1f)
            {
                u = -2f - u;
                v = -v;
            }

            if (v > 1f)
            {
                v = 2f - v;
                u = -u;
            }
            else if (v < -1f)
            {
                v = -2f - v;
                u = -u;
            }
        }

        /// <summary>The direction at the centre of texel (column, row) of a map with a one-texel border.</summary>
        public static void TexelDirection(int column, int row, int size, out float x, out float y, out float z)
        {
            int inner = size - 2;
            float u = (column - 0.5f) / inner * 2f - 1f;
            float v = (row - 0.5f) / inner * 2f - 1f;
            Fold(ref u, ref v);
            Decode(u, v, out x, out y, out z);
        }

        /// <summary>Texture coordinates in 0..1 for a unit direction, as the shader computes them.</summary>
        public static void TextureCoordinates(float x, float y, float z, int size, out float s, out float t)
        {
            Encode(x, y, z, out float u, out float v);
            float texel = 1f / size;
            s = (u * 0.5f + 0.5f) * (1f - 2f * texel) + texel;
            t = (v * 0.5f + 0.5f) * (1f - 2f * texel) + texel;
        }
    }
}
