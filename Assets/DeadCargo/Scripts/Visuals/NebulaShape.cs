using System;
using DeadCargo.Sectors;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// The settings that decide where the sky's nebula is. Changing any of them needs a new bake.
    /// Colours, steps and opacity are applied live by the shader and never need one.
    /// </summary>
    public readonly struct NebulaShape
    {
        public NebulaShape(int seed, float scale, int octaves, float swirl, float bandWidth, float bandFocus, float hueScale)
        {
            Seed = seed;
            Scale = scale;
            Octaves = Math.Max(1, octaves);
            Swirl = swirl;
            BandWidth = Math.Max(0.01f, bandWidth);
            BandFocus = bandFocus;
            HueScale = hueScale;
        }

        public int Seed { get; }

        /// <summary>Size of the largest cloud shapes. Higher gives more, smaller clouds.</summary>
        public float Scale { get; }

        /// <summary>Noise octaves: how much fine detail the cloud edges carry.</summary>
        public int Octaves { get; }

        /// <summary>How strongly the clouds are twisted by a second noise field.</summary>
        public float Swirl { get; }

        /// <summary>Half-thickness of the band the clouds gather in, as a fraction of the sky's height.</summary>
        public float BandWidth { get; }

        /// <summary>0 spreads clouds over the whole sky; 1 keeps them to the band.</summary>
        public float BandFocus { get; }

        /// <summary>Size of the regions that switch between the two nebula palettes.</summary>
        public float HueScale { get; }

        /// <summary>The same shape with another seed: the same kind of nebula, in a different place.</summary>
        public NebulaShape WithSeed(int seed)
        {
            return new NebulaShape(seed, Scale, Octaves, Swirl, BandWidth, BandFocus, HueScale);
        }

        /// <summary>A hash of every setting, the map size and the field's version, stored with a bake to tell when it is out of date.</summary>
        public int ComputeHash(int mapSize)
        {
            uint hash = SectorHash.Mix((uint)NebulaField.Version);
            hash = SectorHash.Combine(hash, unchecked((uint)Seed));
            hash = SectorHash.Combine(hash, Bits(Scale));
            hash = SectorHash.Combine(hash, (uint)Octaves);
            hash = SectorHash.Combine(hash, Bits(Swirl));
            hash = SectorHash.Combine(hash, Bits(BandWidth));
            hash = SectorHash.Combine(hash, Bits(BandFocus));
            hash = SectorHash.Combine(hash, Bits(HueScale));
            hash = SectorHash.Combine(hash, (uint)mapSize);
            return unchecked((int)hash);
        }

        private static uint Bits(float value)
        {
            return unchecked((uint)BitConverter.SingleToInt32Bits(value));
        }
    }
}
