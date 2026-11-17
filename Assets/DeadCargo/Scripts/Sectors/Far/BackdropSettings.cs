namespace DeadCargo.Sectors.Far
{
    /// <summary>
    /// The rules for a run's backdrop bodies as plain values, as <see cref="BackdropLayout"/> reads them. Sizes and angles
    /// are in degrees of sky; sizes are measured across the body.
    /// </summary>
    public struct BackdropSettings
    {
        /// <summary>Chance of a primary planet, 0 to 1. Without one there are no moons.</summary>
        public float PrimaryChance;

        public float PrimaryMinSize;
        public float PrimaryMaxSize;

        /// <summary>Least angle between the primary's centre and the sun's. Low shows its night side.</summary>
        public float PrimaryMinFromSun;

        /// <summary>Greatest angle between the primary's centre and the sun's.</summary>
        public float PrimaryMaxFromSun;

        public int MinMoons;
        public int MaxMoons;
        public float MoonMinSize;
        public float MoonMaxSize;

        /// <summary>Least angle from the primary's edge to a moon's centre.</summary>
        public float MoonMinGap;

        /// <summary>Greatest angle from the primary's edge to a moon's centre.</summary>
        public float MoonMaxGap;

        public int MinDistant;
        public int MaxDistant;
        public float DistantMinSize;
        public float DistantMaxSize;

        /// <summary>Highest the primary and distant planets sit above or below the sector's plane.</summary>
        public float MaxElevation;

        /// <summary>Least gap between the sun's edge, corona included, and any body's edge.</summary>
        public float SunClearance;

        /// <summary>Least gap between the edges of any two bodies.</summary>
        public float BodyClearance;

        /// <summary>Least gap between where the player first looks and the primary's edge. 0 lets it sit anywhere.</summary>
        public float StartViewClearance;

        /// <summary>Greatest tilt of a body's spin axis from the sector's up.</summary>
        public float MaxTilt;

        /// <summary>Slowest spin, in degrees a second.</summary>
        public float MinSpin;

        /// <summary>Fastest spin, in degrees a second.</summary>
        public float MaxSpin;

        /// <summary>The rules Polaris uses: two planets a run, the massive primary and one smaller planet elsewhere in the sky.</summary>
        public static BackdropSettings Default => new BackdropSettings
        {
            PrimaryChance = 1f,
            PrimaryMinSize = 16f,
            PrimaryMaxSize = 34f,
            PrimaryMinFromSun = 60f,
            PrimaryMaxFromSun = 160f,
            MinMoons = 0,
            MaxMoons = 0,
            MoonMinSize = 2f,
            MoonMaxSize = 6f,
            MoonMinGap = 2f,
            MoonMaxGap = 16f,
            MinDistant = 1,
            MaxDistant = 1,
            DistantMinSize = 3f,
            DistantMaxSize = 8f,
            MaxElevation = 40f,
            SunClearance = 6f,
            BodyClearance = 2f,
            StartViewClearance = 0f,
            MaxTilt = 25f,
            MinSpin = 0.02f,
            MaxSpin = 0.12f
        };
    }
}
