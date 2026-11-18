namespace DeadCargo.Flow
{
    /// <summary>
    /// One named piece of setup work in a load report: the main-thread time measured under its label, and the time any
    /// work under it ran over frames, such as jobs on worker threads.
    /// </summary>
    public readonly struct LoadWork
    {
        public LoadWork(string label, double totalMilliseconds, int calls, double maxMilliseconds, double spanSeconds)
        {
            Label = label;
            TotalMilliseconds = totalMilliseconds;
            Calls = calls;
            MaxMilliseconds = maxMilliseconds;
            SpanSeconds = spanSeconds;
        }

        public string Label { get; }

        /// <summary>Main-thread time measured under the label, in milliseconds.</summary>
        public double TotalMilliseconds { get; }

        /// <summary>How many times it was measured: once for one-off setup, every frame for streaming.</summary>
        public int Calls { get; }

        /// <summary>The longest single measurement, in milliseconds.</summary>
        public double MaxMilliseconds { get; }

        /// <summary>Time from start to finish of work that ran over frames, in seconds. Zero when it never did.</summary>
        public double SpanSeconds { get; }
    }
}
