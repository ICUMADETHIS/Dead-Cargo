using UnityEngine;

namespace DeadCargo.Flow
{
    /// <summary>
    /// One piece of a new scene's setup that the loading screen waits for before it shows the scene (phase 6.6). Made by
    /// <see cref="LoadGate.Begin"/>. Its owner reports progress and completes it. A task begun while no load is waiting
    /// is not tracked, so the same code runs unchanged in play.
    /// </summary>
    public sealed class LoadTask
    {
        internal LoadTask(string label, bool isTracked, double started)
        {
            Label = label;
            Banner = label.ToUpperInvariant();
            IsTracked = isTracked;
            Started = started;
        }

        /// <summary>A short name for what is being set up, such as "Building field". The loading screen shows it.</summary>
        public string Label { get; }

        /// <summary>How far the task has got, from 0 to 1. Never goes back.</summary>
        public float Progress { get; private set; }

        public bool IsDone { get; private set; }

        /// <summary>True when a loading screen is waiting for this task.</summary>
        public bool IsTracked { get; }

        // The label as the loading screen's stage line shows it, made once.
        internal string Banner { get; }

        internal double Started { get; }
        internal double Finished { get; private set; } = double.NaN;

        /// <summary>Reports how far the task has got, from 0 to 1. Lower values than the last are ignored.</summary>
        public void Report(float progress)
        {
            if (!IsDone)
                Progress = Mathf.Max(Progress, Mathf.Clamp01(progress));
        }

        /// <summary>Marks the task finished, or abandoned, so the loading screen stops waiting for it. Safe to call twice.</summary>
        public void Complete()
        {
            if (IsDone)
                return;

            IsDone = true;
            Progress = 1f;
            Finished = Time.realtimeSinceStartupAsDouble;
        }
    }
}
