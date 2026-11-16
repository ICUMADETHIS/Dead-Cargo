namespace DeadCargo.Extraction
{
    /// <summary>How a run ended.</summary>
    public enum RunOutcome
    {
        /// <summary>The run ship jumped out. Its hold is banked.</summary>
        Extracted,

        /// <summary>The run ship was destroyed or the player died. The hold is gone.</summary>
        Lost
    }
}
