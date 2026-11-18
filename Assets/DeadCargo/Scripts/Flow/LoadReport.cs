using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace DeadCargo.Flow
{
    /// <summary>
    /// Times every scene change and the seconds after it (phase 6.6). The loading screen marks its stages. Systems with
    /// setup work wrap it in <see cref="Measure"/>, and open a span (<see cref="StartSpan"/>) for work that runs over frames,
    /// such as jobs. From the moment the game appears, frames are counted for <see cref="WatchSeconds"/>, and each slow
    /// frame is blamed on the measured work that ran longest in it. Then the report goes to the console, the Player.log
    /// in a build, and is kept as <see cref="Last"/>. The game's first scene is watched the same way from its first frame.
    /// Outside a report, measured work that runs long is logged as a hitch, and work built on demand in play can ask for
    /// the frames after it to be watched (<see cref="WatchPlay"/>). Measuring is two clock reads and allocates nothing;
    /// main thread only.
    /// </summary>
    public static class LoadReport
    {
        /// <summary>How long frames are watched after the game appears, in seconds.</summary>
        public const float WatchSeconds = 10f;

        /// <summary>How long frames are watched after work built on demand in play, in seconds.</summary>
        public const float PlayWatchSeconds = 3f;

        private const double SlowFrame = 33.0;
        private const double VerySlowFrame = 50.0;
        private const double HitchMilliseconds = 25.0;
        private const float HitchLogInterval = 10f;
        private const int MaxLabels = 32;
        private const string CollectionLabel = "garbage collection";

        // The new scene's objects start up over its first two frames.
        private const int FirstFrameCount = 2;

        private static readonly List<Entry> Entries = new List<Entry>(MaxLabels);
        private static readonly List<LoadWait> Waits = new List<LoadWait>(8);
        private static readonly FrameTally LoadFrames = new FrameTally();
        private static readonly FrameTally PlayFrames = new FrameTally();

        private static LoadWatcher watcher;
        private static bool recording;
        private static bool firstScene;
        private static string scene;
        private static double begun;
        private static double fadedOut;
        private static double loaded;
        private static double activating;
        private static double woken;
        private static double switched;
        private static double tasksDone;
        private static double gateEnded;
        private static bool settled;
        private static string heldBy;
        private static double fadeInStarted;
        private static double fadedIn;
        private static double watchFrom;
        private static double watchUntil;
        private static int firstFramesLeft;
        private static double firstFrames;
        private static int lastCollectionCount;

        // The watch after work built in play: its label, how long the work took, and when the watch began.
        private static string playLabel;
        private static double playMilliseconds;
        private static double playFrom;
        private static string intervalLabel;
        private static double intervalMilliseconds;

        /// <summary>Raised when a report is finished, with the report.</summary>
        public static event Action<LoadRecord> Finished;

        /// <summary>The last finished report, or null.</summary>
        public static LoadRecord Last { get; private set; }

        /// <summary>True from the start of a load until its watch ends.</summary>
        public static bool IsRecording => recording;

        private static double Now => Time.realtimeSinceStartupAsDouble;

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Entries.Clear();
            Waits.Clear();
            LoadFrames.Reset();
            PlayFrames.Reset();
            playLabel = null;
            watcher = null;
            recording = false;
            Last = null;
            Finished = null;
        }

        // The game's first scene has no loading screen. Its objects have woken but not yet started.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void WatchFirstScene()
        {
            Begin(SceneManager.GetActiveScene().name, true);
            watchFrom = Now;
            watchUntil = watchFrom + WatchSeconds;
            firstFramesLeft = FirstFrameCount;
        }

        /// <summary>Measures the work in a <c>using</c> block under a label. Labels are constants, a few per system.</summary>
        public static LoadScope Measure(string label)
        {
            return new LoadScope(label);
        }

        /// <summary>Marks the start of work that runs over frames, such as jobs on worker threads.</summary>
        public static void StartSpan(string label)
        {
            Entry entry = Find(label);
            if (entry != null)
                entry.SpanStart = Now;
        }

        /// <summary>Marks the end of work started with <see cref="StartSpan"/>. Does nothing when none is open.</summary>
        public static void EndSpan(string label)
        {
            Entry entry = Find(label);
            if (entry == null || double.IsNaN(entry.SpanStart))
                return;

            if (recording)
                entry.SpanSeconds += Now - entry.SpanStart;
            entry.SpanStart = double.NaN;
        }

        /// <summary>
        /// Watches the frames for <see cref="PlayWatchSeconds"/> after work built on demand in play, such as a planet's
        /// ground copy, then logs how long the work took and the worst frame that followed, which shows any cost the work
        /// left to later frames. Call right after the work measured under <paramref name="label"/>. Does nothing during a
        /// load report, which counts those frames itself.
        /// </summary>
        public static void WatchPlay(string label)
        {
            if (recording)
                return;

            Entry entry = Find(label);
            if (entry == null)
                return;

            if (playLabel != null)
                FinishPlay();

            EnsureWatcher();
            playLabel = entry.Label;
            playMilliseconds = entry.LastMilliseconds;
            playFrom = Now;
            PlayFrames.Reset();

            // The work ran in this frame, so the frame is blamed on it.
            if (playMilliseconds > intervalMilliseconds)
            {
                intervalMilliseconds = playMilliseconds;
                intervalLabel = entry.Label;
            }
        }

        internal static void BeginLoad(string sceneName)
        {
            Begin(sceneName, false);
        }

        internal static void MarkFadedOut()
        {
            fadedOut = Now;
        }

        /// <summary>The scene's data is in, and it can be switched on.</summary>
        internal static void MarkLoaded()
        {
            loaded = Now;
        }

        /// <summary>The still frames are drawn, and the loaded scene is let switch on.</summary>
        internal static void MarkActivating()
        {
            activating = Now;
        }

        /// <summary>The new scene's objects have woken (Awake and OnEnable); their Start has not run yet.</summary>
        internal static void MarkWoken()
        {
            woken = Now;
        }

        /// <summary>
        /// The loading screen sees the switch done. Unity has run the new scene's Start and first Update by then, in the
        /// same frame as the switch.
        /// </summary>
        internal static void MarkSwitched()
        {
            switched = Now;
            firstFramesLeft = FirstFrameCount;
        }

        /// <summary>No load task is running. Marked again if a later task holds the screen once more.</summary>
        internal static void MarkTasksDone()
        {
            tasksDone = Now;
        }

        /// <summary>The loading screen has stopped waiting: the frames settled, or did not in the time allowed.</summary>
        internal static void MarkSettled(bool hasSettled)
        {
            gateEnded = Now;
            settled = hasSettled;
        }

        /// <summary>The loading screen reached its time limit with a task still running.</summary>
        internal static void MarkHeldBy(string label)
        {
            gateEnded = Now;
            settled = false;
            heldBy = label;
        }

        internal static void MarkTask(string label, double seconds, bool finished)
        {
            if (recording)
                Waits.Add(new LoadWait(label, seconds, finished));
        }

        internal static void MarkFadeIn()
        {
            fadeInStarted = Now;
            watchFrom = fadeInStarted;
        }

        internal static void MarkFadedIn()
        {
            fadedIn = Now;
            watchUntil = fadedIn + WatchSeconds;
        }

        /// <summary>Drops a load that failed, without a report.</summary>
        internal static void Cancel()
        {
            recording = false;
        }

        internal static void EndScope(string label, long started)
        {
            double milliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            Entry entry = Find(label);
            if (entry == null)
                return;

            entry.LastMilliseconds = milliseconds;
            if (milliseconds > intervalMilliseconds)
            {
                intervalMilliseconds = milliseconds;
                intervalLabel = entry.Label;
            }

            if (recording)
            {
                entry.Total += milliseconds;
                entry.Calls++;
                entry.Max = Math.Max(entry.Max, milliseconds);
                return;
            }

            if (milliseconds < HitchMilliseconds || Time.unscaledTime - entry.LastHitch < HitchLogInterval)
                return;

            entry.LastHitch = Time.unscaledTime;
            Debug.Log($"[LoadReport] {label} took {milliseconds:0} ms during play.");
        }

        /// <summary>
        /// Counts the frame that has just ended, blaming it on the measured work that ran longest in it. Called once a
        /// frame by the watcher, before any other script, so everything measured since the last call ran in that frame.
        /// </summary>
        internal static void Tick(float deltaTime)
        {
            // A garbage collection finished in the frame just ended. Read every frame, so the next frame's change is its own.
            int collectionCount = GC.CollectionCount(0);
            bool collected = collectionCount != lastCollectionCount;
            lastCollectionCount = collectionCount;

            double milliseconds = deltaTime * 1000.0;
            if (recording)
                CountFrame(deltaTime, collected);
            else if (playLabel != null)
                PlayFrames.Count(milliseconds, Now - playFrom, collected, Blame(PlayFrames, milliseconds));

            intervalLabel = null;
            intervalMilliseconds = 0.0;
            if (recording && !double.IsNaN(watchUntil) && Now >= watchUntil)
                Finish();
            if (playLabel != null && Now >= playFrom + PlayWatchSeconds)
                FinishPlay();
        }

        private static void CountFrame(float deltaTime, bool collected)
        {
            if (firstFramesLeft > 0)
            {
                firstFramesLeft--;
                firstFrames = Math.Max(firstFrames, deltaTime);
            }

            if (double.IsNaN(watchFrom) || Now <= watchFrom)
                return;

            double milliseconds = deltaTime * 1000.0;
            LoadFrames.Count(milliseconds, Now - watchFrom, collected, Blame(LoadFrames, milliseconds));
        }

        // Only a new worst frame needs its blame, so the open spans are searched only then.
        private static string Blame(FrameTally tally, double milliseconds)
        {
            return milliseconds > tally.Worst ? intervalLabel ?? OpenSpan() : null;
        }

        private static void Begin(string sceneName, bool isFirstScene)
        {
            if (recording)
                Finish();
            if (playLabel != null)
                FinishPlay();

            EnsureWatcher();
            recording = true;
            firstScene = isFirstScene;
            scene = sceneName;
            begun = Now;
            fadedOut = loaded = activating = woken = switched = tasksDone = gateEnded = fadeInStarted = fadedIn = watchFrom = watchUntil = double.NaN;
            settled = false;
            heldBy = null;
            Waits.Clear();
            firstFramesLeft = 0;
            firstFrames = 0.0;
            LoadFrames.Reset();
            intervalLabel = null;
            intervalMilliseconds = 0.0;
            foreach (Entry entry in Entries)
                entry.Reset();
        }

        private static void Finish()
        {
            recording = false;
            var work = new List<LoadWork>(Entries.Count);
            foreach (Entry entry in Entries)
            {
                if (entry.Calls > 0 || entry.SpanSeconds > 0.0)
                    work.Add(new LoadWork(entry.Label, entry.Total, entry.Calls, entry.Max, entry.SpanSeconds));
            }

            var record = new LoadRecord
            {
                Scene = scene,
                IsFirstScene = firstScene,
                FadeOut = Between(begun, fadedOut),
                Load = Between(fadedOut, loaded),
                Entering = Between(loaded, activating),
                Switch = Between(activating, double.IsNaN(woken) ? switched : woken),
                StartUp = Between(woken, switched),
                FirstFrames = firstFrames,
                TaskWait = Between(switched, tasksDone),
                Settling = Between(tasksDone, gateEnded),
                Settled = settled,
                HeldBy = heldBy,
                Waiting = Between(switched, fadeInStarted),
                FadeIn = Between(fadeInStarted, fadedIn),
                UntilShown = Between(begun, fadeInStarted),
                Waits = Waits.ToArray(),
                Work = work,
                Frames = LoadFrames.Frames,
                AverageMilliseconds = LoadFrames.Average,
                WorstMilliseconds = LoadFrames.Worst,
                WorstLabel = LoadFrames.WorstLabel,
                WorstAt = LoadFrames.WorstAt,
                WorstCollected = LoadFrames.WorstCollected,
                Collections = LoadFrames.Collections,
                Over33 = LoadFrames.Over33,
                Over50 = LoadFrames.Over50
            };

            record.Summary = Summarise(record);
            Last = record;
            Debug.Log(record.Summary);
            Finished?.Invoke(record);
        }

        // One line: the work, then the frames that followed it.
        private static void FinishPlay()
        {
            FrameTally tally = PlayFrames;
            var text = new StringBuilder(256);
            text.Append("[LoadReport] ").Append(playLabel).Append(" built in ").Append(playMilliseconds.ToString("0")).Append(" ms during play; the next ")
                .Append(PlayWatchSeconds.ToString("0")).Append(" s: ").Append(tally.Frames).Append(" frames, worst ")
                .Append(tally.Worst.ToString("0")).Append(" ms at ").Append(Seconds(tally.WorstAt));
            AppendBlame(text, tally.WorstLabel, tally.WorstCollected);
            text.Append(", ").Append(tally.Over50).Append(" over 50 ms, ").Append(tally.Over33).Append(" over 33 ms");

            playLabel = null;
            Debug.Log(text.ToString());
        }

        private static void AppendBlame(StringBuilder text, string label, bool collected)
        {
            if (label == null && !collected)
                return;

            text.Append(" (").Append(label);
            if (collected)
                text.Append(label != null ? ", " : "").Append(CollectionLabel);
            text.Append(')');
        }

        private static string Summarise(LoadRecord record)
        {
            var text = new StringBuilder(640);
            text.Append("[LoadReport] ").Append(record.Scene);
            if (record.IsFirstScene)
            {
                text.Append(" started").AppendLine();
                text.Append("  First frames: ").Append(Seconds(record.FirstFrames)).AppendLine();
            }
            else
            {
                text.Append(" loaded, shown after ").Append(Seconds(record.UntilShown)).AppendLine();
                text.Append("  Stages: fade out ").Append(Seconds(record.FadeOut)).Append(", load ").Append(Seconds(record.Load))
                    .Append(", entering ").Append(Seconds(record.Entering)).Append(", switch ").Append(Seconds(record.Switch))
                    .Append(", start-up ").Append(Seconds(record.StartUp)).Append(" (first frames ").Append(Seconds(record.FirstFrames))
                    .Append("), waiting ").Append(Seconds(record.Waiting))
                    .Append(", fade in ").Append(Seconds(record.FadeIn)).AppendLine();
                AppendGate(text, record);
            }

            IReadOnlyList<LoadWork> work = record.Work;
            text.Append("  Setup: ");
            if (work.Count == 0)
                text.Append("nothing measured");

            for (int i = 0; i < work.Count; ++i)
            {
                LoadWork piece = work[i];
                if (i > 0)
                    text.Append("; ");

                text.Append(piece.Label);
                if (piece.Calls == 1)
                    text.Append(' ').Append(piece.TotalMilliseconds.ToString("0")).Append(" ms");
                else if (piece.Calls > 1)
                    text.Append(' ').Append(piece.TotalMilliseconds.ToString("0")).Append(" ms over ").Append(piece.Calls)
                        .Append(" calls, longest ").Append(piece.MaxMilliseconds.ToString("0.0")).Append(" ms");

                if (piece.SpanSeconds > 0.0)
                    text.Append(piece.Calls > 0 ? ", " : " ").Append("ran for ").Append(Seconds(piece.SpanSeconds));
            }

            double average = record.AverageMilliseconds;
            text.AppendLine();
            text.Append("  First ").Append(WatchSeconds.ToString("0")).Append(" s shown: ").Append(record.Frames).Append(" frames, average ")
                .Append(average.ToString("0.0")).Append(" ms");
            if (average > 0.0)
                text.Append(" (").Append((1000.0 / average).ToString("0")).Append(" fps)");

            text.Append(", worst ").Append(record.WorstMilliseconds.ToString("0")).Append(" ms at ").Append(Seconds(record.WorstAt));
            AppendBlame(text, record.WorstLabel, record.WorstCollected);
            text.Append(", ").Append(record.Over50).Append(" over 50 ms, ").Append(record.Over33).Append(" over 33 ms, ")
                .Append(record.Collections).Append(" garbage collections");
            return text.ToString();
        }

        // What the loading screen waited for after the switch, and how it stopped waiting.
        private static void AppendGate(StringBuilder text, LoadRecord record)
        {
            text.Append("  Gate: ");
            if (record.HeldBy != null)
                text.Append("gave up after ").Append(Seconds(record.Waiting)).Append(", held by ").Append(record.HeldBy).Append("; ");

            if (record.Waits.Count == 0)
            {
                text.Append("no tasks");
            }
            else
            {
                text.Append("tasks ").Append(Seconds(record.TaskWait)).Append(" (");
                for (int i = 0; i < record.Waits.Count; ++i)
                {
                    LoadWait wait = record.Waits[i];
                    if (i > 0)
                        text.Append(", ");

                    text.Append(wait.Label).Append(' ').Append(Seconds(wait.Seconds));
                    if (!wait.Finished)
                        text.Append(" unfinished");
                }

                text.Append(')');
            }

            if (record.HeldBy == null)
                text.Append(record.Settled ? ", settled after " : ", did not settle in ").Append(Seconds(record.Settling));
            text.AppendLine();
        }

        private static string Seconds(double seconds)
        {
            return seconds < 0.0 ? "-" : seconds.ToString("0.00") + " s";
        }

        // A stage the load did not have reads as negative.
        private static double Between(double from, double to)
        {
            return double.IsNaN(from) || double.IsNaN(to) ? -1.0 : Math.Max(0.0, to - from);
        }

        private static string OpenSpan()
        {
            foreach (Entry entry in Entries)
            {
                if (!double.IsNaN(entry.SpanStart))
                    return entry.Label;
            }

            return null;
        }

        private static Entry Find(string label)
        {
            if (label == null)
                return null;

            for (int i = 0; i < Entries.Count; ++i)
            {
                if (ReferenceEquals(Entries[i].Label, label) || string.Equals(Entries[i].Label, label, StringComparison.Ordinal))
                    return Entries[i];
            }

            if (Entries.Count >= MaxLabels)
                return null;

            var entry = new Entry(label);
            Entries.Add(entry);
            return entry;
        }

        private static void EnsureWatcher()
        {
            if (watcher != null)
                return;

            var holder = new GameObject("Load Report") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(holder);
            watcher = holder.AddComponent<LoadWatcher>();
        }

        private sealed class Entry
        {
            internal Entry(string label)
            {
                Label = label;
            }

            internal string Label { get; }
            internal double Total;
            internal int Calls;
            internal double Max;
            internal double SpanSeconds;
            internal double SpanStart = double.NaN;
            internal float LastHitch = float.NegativeInfinity;

            // The latest measurement, kept outside a report too, for WatchPlay.
            internal double LastMilliseconds;

            internal void Reset()
            {
                Total = 0.0;
                Calls = 0;
                Max = 0.0;
                SpanSeconds = 0.0;
            }
        }

        // The frames of one watch: how many, how long, and the worst with when it came and what ran in it.
        private sealed class FrameTally
        {
            internal int Frames;
            internal double Sum;
            internal double Worst;
            internal string WorstLabel;
            internal double WorstAt;
            internal bool WorstCollected;
            internal int Over33;
            internal int Over50;
            internal int Collections;

            internal double Average => Frames > 0 ? Sum / Frames : 0.0;

            internal void Reset()
            {
                Frames = Over33 = Over50 = Collections = 0;
                Sum = Worst = WorstAt = 0.0;
                WorstLabel = null;
                WorstCollected = false;
            }

            // A blame is given only for a new worst frame.
            internal void Count(double milliseconds, double at, bool collected, string blame)
            {
                Frames++;
                Sum += milliseconds;
                if (collected)
                    Collections++;

                if (milliseconds > Worst)
                {
                    Worst = milliseconds;
                    WorstLabel = blame;
                    WorstAt = at;
                    WorstCollected = collected;
                }

                if (milliseconds > SlowFrame)
                    Over33++;
                if (milliseconds > VerySlowFrame)
                    Over50++;
            }
        }
    }
}
