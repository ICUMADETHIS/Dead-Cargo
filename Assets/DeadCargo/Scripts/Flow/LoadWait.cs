namespace DeadCargo.Flow
{
    /// <summary>How long one load task (<see cref="LoadTask"/>) kept the loading screen waiting, in a load report.</summary>
    public readonly struct LoadWait
    {
        public LoadWait(string label, double seconds, bool finished)
        {
            Label = label;
            Seconds = seconds;
            Finished = finished;
        }

        public string Label { get; }

        /// <summary>From the task's start until it finished, or until the loading screen gave up on it, in seconds.</summary>
        public double Seconds { get; }

        /// <summary>False when the loading screen gave up waiting for it.</summary>
        public bool Finished { get; }
    }
}
