namespace DeadCargo.Flight
{
    /// <summary>How a planet's air shapes flight near it, as plain values for <see cref="PlanetPhysics"/>.</summary>
    public struct AirSettings
    {
        /// <summary>The ship's top speed at the ground, as a multiple of its speed in open space.</summary>
        public double GroundFactor;

        /// <summary>The ship's top speed at the top of the air and above it, as a multiple of its speed in open space.</summary>
        public double TopFactor;

        /// <summary>How the speed falls as the air thickens: 1 evenly with height, higher keeps it fast for longer.</summary>
        public double Curve;

        /// <summary>
        /// How far above the top of the air the planet's pull and the fast flight reach, in metres. The speed eases back to
        /// open space over the outer half.
        /// </summary>
        public double Range;

        /// <summary>Glow from heating at this value: air density times speed in kilometres a second, cubed.</summary>
        public double GlowStart;

        /// <summary>Full glow from heating at this value.</summary>
        public double GlowFull;

        /// <summary>The values the descent was tuned with: about a minute from where cruise ends to the ground.</summary>
        public static AirSettings Default => new AirSettings
        {
            GroundFactor = 1.5,
            TopFactor = 25.0,
            Curve = 1.0,
            Range = 40000.0,
            GlowStart = 0.06,
            GlowFull = 0.13
        };
    }
}
