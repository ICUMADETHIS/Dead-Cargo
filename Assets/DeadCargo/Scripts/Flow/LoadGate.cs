using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.Flow
{
    /// <summary>
    /// What the loading screen waits for before it shows a new scene (phase 6.6). The gate is open from the moment the
    /// new scene is switched on until the loading screen starts to show it. While it is open, systems whose setup runs
    /// over frames, such as jobs on workers, register a <see cref="LoadTask"/> and complete it when done. Outside a load
    /// the tasks they register are not tracked. Main thread only.
    /// </summary>
    public static class LoadGate
    {
        private static readonly List<LoadTask> Tracked = new List<LoadTask>(8);

        /// <summary>True while a new scene is live behind the loading screen and has not yet been shown.</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>The first tracked task still running, or null when there is none.</summary>
        internal static LoadTask Pending
        {
            get
            {
                for (int i = 0; i < Tracked.Count; ++i)
                {
                    if (!Tracked[i].IsDone)
                        return Tracked[i];
                }

                return null;
            }
        }

        /// <summary>The tracked tasks' mean progress, from 0 to 1; 1 when there are none.</summary>
        internal static float Progress
        {
            get
            {
                if (Tracked.Count == 0)
                    return 1f;

                float sum = 0f;
                for (int i = 0; i < Tracked.Count; ++i)
                    sum += Tracked[i].Progress;
                return sum / Tracked.Count;
            }
        }

        /// <summary>
        /// Starts a task under a short constant label. The loading screen waits for it while the gate is open; otherwise
        /// it is returned untracked.
        /// </summary>
        public static LoadTask Begin(string label)
        {
            var task = new LoadTask(label, IsOpen, Time.realtimeSinceStartupAsDouble);
            if (IsOpen)
                Tracked.Add(task);
            return task;
        }

        internal static void Open()
        {
            Tracked.Clear();
            IsOpen = true;
        }

        /// <summary>Stops tracking, and gives the load report how long each task kept the screen waiting.</summary>
        internal static void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            double now = Time.realtimeSinceStartupAsDouble;
            foreach (LoadTask task in Tracked)
                LoadReport.MarkTask(task.Label, (task.IsDone ? task.Finished : now) - task.Started, task.IsDone);
            Tracked.Clear();
        }

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Tracked.Clear();
            IsOpen = false;
        }
    }
}
