using UnityEngine;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// Turns a nebula shape into the two-channel map the Space Sky shader reads: density and hue as half floats,
    /// in an octahedral layout with a one-texel border. Used by the editor to save a sky's map, and at load to bake
    /// a fresh one.
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
        /// Bakes a shape into a new texture. A map kept readable can be saved as an asset; one that is not frees its
        /// CPU copy once uploaded, which is what a map made at runtime wants.
        /// </summary>
        public static Texture2D Create(NebulaShape shape, string name, bool keepReadable)
        {
            float[] texels = NebulaBaker.Bake(shape);
            var halves = new ushort[texels.Length];
            for (int i = 0; i < texels.Length; ++i)
                halves[i] = Mathf.FloatToHalf(texels[i]);

            int size = NebulaBaker.MapSize;
            var map = new Texture2D(size, size, TextureFormat.RGHalf, false, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };
            map.SetPixelData(halves, 0);
            map.Apply(false, !keepReadable);
            return map;
        }
    }
}
