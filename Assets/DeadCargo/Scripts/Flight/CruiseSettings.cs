namespace DeadCargo.Flight
{
    /// <summary>How the cruise drive's speed follows the space around it, as plain values for <see cref="CruiseMath"/>.</summary>
    public struct CruiseSettings
    {
        /// <summary>Speed for each metre to the nearest surface, per second.</summary>
        public double Rate;

        /// <summary>Slowest cruise, in metres a second.</summary>
        public double MinSpeed;

        /// <summary>Fastest cruise, in metres a second.</summary>
        public double MaxSpeed;

        /// <summary>How quickly the speed follows its target, per second.</summary>
        public double Response;

        /// <summary>Height above a planet's surface where cruise ends, in metres.</summary>
        public double DropHeight;

        /// <summary>The values the drive was designed with: Polaris to 3 km above Ixara in under a minute.</summary>
        public static CruiseSettings Default => new CruiseSettings
        {
            Rate = 0.2,
            MinSpeed = 2500.0,
            MaxSpeed = 400000.0,
            Response = 1.5,
            DropHeight = 3000.0
        };
    }
}
