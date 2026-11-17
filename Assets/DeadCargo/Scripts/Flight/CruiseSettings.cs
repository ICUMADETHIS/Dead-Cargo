namespace DeadCargo.Flight
{
    /// <summary>How the autopilot turns and how fast it cruises, as plain values for <see cref="CruiseAutopilot"/>.</summary>
    public struct CruiseSettings
    {
        /// <summary>
        /// Speed towards a surface for each metre to it, per second. 0.2 leaves five seconds to reach it, so the drive
        /// brakes smoothly into every arrival.
        /// </summary>
        public double ApproachRate;

        /// <summary>Speed for each metre of clearance while not closing on a surface, per second: leaving, or passing by.</summary>
        public double CrossRate;

        /// <summary>Slowest cruise outside the sector's reach, in metres a second.</summary>
        public double MinSpeed;

        /// <summary>Fastest cruise, in metres a second.</summary>
        public double MaxSpeed;

        /// <summary>Cruise speed within the sector's reach, where the ship really flies past rocks, in metres a second.</summary>
        public double SectorSpeed;

        /// <summary>How quickly the speed follows its target, per second.</summary>
        public double Response;

        /// <summary>Fastest turn, in degrees a second.</summary>
        public double TurnRate;

        /// <summary>How a turn settles, per second: near the end it slows to this times the angle left.</summary>
        public double TurnEase;

        /// <summary>The values the autopilot was tuned with: Polaris to the top of Ixara's atmosphere in about 40 seconds.</summary>
        public static CruiseSettings Default => new CruiseSettings
        {
            ApproachRate = 0.2,
            CrossRate = 1.0,
            MinSpeed = 2000.0,
            MaxSpeed = 400000.0,
            SectorSpeed = 4000.0,
            Response = 1.5,
            TurnRate = 90.0,
            TurnEase = 3.0
        };
    }
}
