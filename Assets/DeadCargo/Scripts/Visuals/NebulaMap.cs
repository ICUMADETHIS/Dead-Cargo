using Unity.Mathematics;
using UnityEngine;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// Turns a nebula shape into the two-channel map the Space Sky shader reads: density and hue as half floats,
    /// in an octahedral layout with a one-texel border. Used by the editor to save a sky's map, and at load to bake
    /// a fresh one. Baking the texels is plain maths that may run off the main thread; making the texture may not.
    /// </summary>
    public static class NebulaMap
    {
        /// <summary>The nebula shape set on a Space Sky material.</summary>
        public static NebulaShape ReadShape(Material material)
        {
            return new NebulaShape(
                Mathf.RoundToInt(material.GetFloat(SpaceSkyProperties.NebulaSeed)),
                material.GetFloat(SpaceSkyProperties.NebulaScale),
                Mathf.RoundToInt(material.GetFloat(SpaceSkyProperties.NebulaOctaves)),
                material.GetFloat(SpaceSkyProperties.NebulaSwirl),
                material.GetFloat(SpaceSkyProperties.NebulaBandWidth),
                material.GetFloat(SpaceSkyProperties.NebulaBandFocus),
                material.GetFloat(SpaceSkyProperties.NebulaHueScale));
        }

        /// <summary>
        /// Bakes a shape into the map's texels as half floats, on up to <paramref name="maxThreads"/> cores (-1 for all).
        /// No Unity calls, so it is safe on a worker thread; pass the result to <see cref="Create(ushort[], string, bool)"/>
        /// on the main thread.
        /// </summary>
        public static ushort[] BakeTexels(NebulaShape shape, int maxThreads = -1)
        {
            float[] texels = NebulaBaker.Bake(shape, NebulaBaker.MapSize, maxThreads);
            var halves = new ushort[texels.Length];
            for (int i = 0; i < texels.Length; ++i)
                halves[i] = (ushort)math.f32tof16(texels[i]);
            return halves;
        }

        /// <summary>
        /// Bakes a shape into a new texture. A map kept readable can be saved as an asset; one that is not frees its
        /// CPU copy once uploaded, which is what a map made at runtime wants.
        /// </summary>
        public static Texture2D Create(NebulaShape shape, string name, bool keepReadable)
        {
            return Create(BakeTexels(shape), name, keepReadable);
        }

        /// <summary>Makes the texture from texels baked by <see cref="BakeTexels"/>. Main thread only.</summary>
        public static Texture2D Create(ushort[] texels, string name, bool keepReadable)
        {
            int size = NebulaBaker.MapSize;
            var map = new Texture2D(size, size, TextureFormat.RGHalf, false, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            map.SetPixelData(texels, 0);
            map.Apply(false, !keepReadable);
            return map;
        }
    }
}
