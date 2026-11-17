namespace DeadCargo.Flight
{
    /// <summary>Why the cruise drive will not engage now.</summary>
    public enum CruiseBlock
    {
        /// <summary>Clear to engage.</summary>
        None,

        /// <summary>No planet or sector within reach of the nose.</summary>
        NoDestination,

        /// <summary>The ship is already where the destination's cruise would end.</summary>
        AlreadyThere,

        /// <summary>An asteroid field lies along the route out of the sector.</summary>
        Field
    }
}
