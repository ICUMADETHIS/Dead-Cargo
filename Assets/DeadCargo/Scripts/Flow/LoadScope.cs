using System;
using System.Diagnostics;

namespace DeadCargo.Flow
{
    /// <summary>Measures the work inside a <c>using</c> block for the load report. Made by <see cref="LoadReport.Measure"/>.</summary>
    public readonly struct LoadScope : IDisposable
    {
        private readonly string label;
        private readonly long started;

        internal LoadScope(string label)
        {
            this.label = label;
            started = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            LoadReport.EndScope(label, started);
        }
    }
}
