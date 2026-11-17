using Unity.Mathematics;

namespace DeadCargo.Flight
{
    /// <summary>A planet as the autopilot sees it: where it is, how big, and how high above it cruise ends.</summary>
    public struct CruiseBody
    {
        /// <summary>Centre in the star system, in metres.</summary>
        public double3 Centre;

        /// <summary>Radius at sea level, in metres. 0 for a body that is not there.</summary>
        public double Radius;

        /// <summary>Height above sea level where cruise ends on arrival, in metres: the top of its atmosphere and a margin.</summary>
        public double ArrivalHeight;

        public bool Exists => Radius > 0.0;

        /// <summary>Distance from the centre where cruise ends, and that a route passing by keeps clear of.</summary>
        public double ArrivalRadius => Radius + ArrivalHeight;
    }

    /// <summary>The sector as the autopilot sees it: its box in the star system and how far beyond it its reach goes.</summary>
    public struct CruiseSector
    {
        public double3 Min;
        public double3 Max;

        /// <summary>Distance past the box where the sector sleeps on leaving and wakes on arriving, in metres.</summary>
        public double Reach;
    }
}
