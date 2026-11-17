namespace DeadCargo.Sectors.Placement
{
    /// <summary>A placement rule as plain values, as <see cref="SectorPlacer"/> reads it.</summary>
    public struct PlacementSettings
    {
        /// <summary>Closest to the arrival point, in metres.</summary>
        public float MinDistance;

        /// <summary>Furthest from the arrival point, in metres.</summary>
        public float MaxDistance;

        /// <summary>Furthest above or below the arrival point, in metres.</summary>
        public float MaxHeight;

        /// <summary>Least region density allowed at the spot.</summary>
        public float MinDensity;

        /// <summary>Most region density allowed at the spot.</summary>
        public float MaxDensity;

        /// <summary>Closest any content placed before it may be, in metres.</summary>
        public float Spacing;

        /// <summary>Radius of the clearing opened round it, in metres. 0 opens none.</summary>
        public float KeepClear;

        /// <summary>Turned to face the arrival point, rather than given a random heading.</summary>
        public bool FaceArrival;
    }
}
