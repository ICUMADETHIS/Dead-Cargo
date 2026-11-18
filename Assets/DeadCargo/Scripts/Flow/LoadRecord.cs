using System.Collections.Generic;

namespace DeadCargo.Flow
{
    /// <summary>
    /// A finished load report: how long each stage of a scene change took, what the loading screen waited for, the setup
    /// work by name, and the frames of the first seconds after the game appeared. Times are in seconds unless named in
    /// milliseconds; a stage the load did not have, such as the fades of the game's first scene, is negative.
    /// </summary>
    public sealed class LoadRecord
    {
        internal LoadRecord()
        {
        }

        public string Scene { get; internal set; }

        /// <summary>True for the scene the game started in, which no loading screen covered.</summary>
        public bool IsFirstScene { get; internal set; }

        public double FadeOut { get; internal set; }

        /// <summary>The scene's data loading in the background, up to the point it can be switched on.</summary>
        public double Load { get; internal set; }

        /// <summary>The still, labelled frames drawn between the load and the switch.</summary>
        public double Entering { get; internal set; }

        /// <summary>
        /// Switching the loaded scene on, until its objects have woken (Awake and OnEnable). The first part of the frozen
        /// frame.
        /// </summary>
        public double Switch { get; internal set; }

        /// <summary>
        /// The new scene's Start and first Update, which Unity runs in the same frozen frame as the switch. The second part
        /// of that frame.
        /// </summary>
        public double StartUp { get; internal set; }

        /// <summary>
        /// The longest of the new scene's first two frames: the whole frozen frame, switch and start-up together, plus the
        /// drawing of it.
        /// </summary>
        public double FirstFrames { get; internal set; }

        /// <summary>From the switch until every load task was done.</summary>
        public double TaskWait { get; internal set; }

        /// <summary>From the tasks being done until the frames settled, or the loading screen stopped waiting for them.</summary>
        public double Settling { get; internal set; }

        /// <summary>True when the frames settled before the game was shown.</summary>
        public bool Settled { get; internal set; }

        /// <summary>The task the loading screen gave up waiting for at its time limit, or null.</summary>
        public string HeldBy { get; internal set; }

        /// <summary>From the switch's end to the fade-in: the tasks, settling and the screen's minimum time.</summary>
        public double Waiting { get; internal set; }

        public double FadeIn { get; internal set; }

        /// <summary>From the start of the load until the game began to appear.</summary>
        public double UntilShown { get; internal set; }

        /// <summary>The load tasks the screen waited for, in the order they began.</summary>
        public IReadOnlyList<LoadWait> Waits { get; internal set; }

        /// <summary>Setup work by name, in the order it was first measured.</summary>
        public IReadOnlyList<LoadWork> Work { get; internal set; }

        /// <summary>Frames counted in the watch after the game appeared.</summary>
        public int Frames { get; internal set; }

        public double AverageMilliseconds { get; internal set; }
        public double WorstMilliseconds { get; internal set; }

        /// <summary>The work that ran longest in the worst frame, or null when no measured work ran in it.</summary>
        public string WorstLabel { get; internal set; }

        /// <summary>When the worst frame came, in seconds after the game appeared.</summary>
        public double WorstAt { get; internal set; }

        /// <summary>True when a garbage collection finished in the worst frame.</summary>
        public bool WorstCollected { get; internal set; }

        /// <summary>Garbage collections that finished in the watch.</summary>
        public int Collections { get; internal set; }

        /// <summary>Frames longer than 33 ms, those over 50 ms included.</summary>
        public int Over33 { get; internal set; }

        public int Over50 { get; internal set; }

        /// <summary>The report as written to the console.</summary>
        public string Summary { get; internal set; }
    }
}
