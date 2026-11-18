namespace DeadCargo.AI
{
    /// <summary>
    /// What an AI pilot is doing. Collision avoidance is not a state: it bends the course in every state
    /// (<see cref="PilotBrain.IsAvoiding"/>).
    /// </summary>
    public enum PilotState
    {
        /// <summary>Circling where it last saw a target, or where it is, until it finds one or gives up.</summary>
        Holding,

        /// <summary>Fighting the target its sensors track: attack runs and break-aways.</summary>
        Engaging,

        /// <summary>Lost its target and flying to where it was last tracked, led by its last motion.</summary>
        Pursuing,

        /// <summary>Ordered off the hunt: flying away until it leaves the scene.</summary>
        Withdrawing,

        /// <summary>Hurt, by its skill's measure: broken off to let its shields recharge before it comes back.</summary>
        Regrouping
    }
}
