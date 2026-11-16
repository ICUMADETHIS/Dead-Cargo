namespace DeadCargo.Extraction
{
    /// <summary>The phases of an extraction point.</summary>
    public enum ExtractionPointState
    {
        /// <summary>Waiting for the player to start the uplink.</summary>
        Dormant,

        /// <summary>The uplink is running. The point opens when it completes.</summary>
        Uplinking,

        /// <summary>Open for a limited window. Flying the run ship in extracts the run.</summary>
        Open
    }
}
