#if UNITY_EDITOR || DEVELOPMENT_BUILD || DDT_ENABLED
using System;
using Unity.Profiling;
using UnityEngine;

namespace DeadCargo.Debugging
{
    /// <summary>The engine counters the overlay reads. Each is optional: one Unity does not provide reads as missing.</summary>
    public enum DebugCounter
    {
        MainThread,
        Batches,
        SetPassCalls,
        DrawCalls,
        Triangles,
        Vertices,
        ShadowCasters,
        TotalUsedMemory,
        TotalReservedMemory,
        ManagedUsedMemory,
        ManagedReservedMemory,
        GraphicsMemory,
        SystemMemory,
        ManagedAllocatedInFrame,
        ManagedAllocationsInFrame,
        ActiveDynamicBodies,
        ActiveKinematicBodies,
        DynamicBodies,
        Contacts
    }

    /// <summary>
    /// Frame timing and engine counters for the overlay. Frame times come from the unscaled delta, so pauses and slow
    /// motion do not distort them. Engine counters (draw calls, memory, physics) come from Unity's profiler counters,
    /// which run in the editor and development builds without the Profiler window open. Allocates nothing per frame.
    /// </summary>
    public sealed class DebugFrameStats : IDisposable
    {
        /// <summary>Frames kept for the graph and the 1% low: about four seconds at 60 fps.</summary>
        public const int HistoryLength = 240;

        // Frames the main-thread counter averages over.
        private const int CounterSamples = 15;

        private static readonly (ProfilerCategory Category, string Name)[] counterSources =
        {
            (ProfilerCategory.Internal, "Main Thread"),
            (ProfilerCategory.Render, "Batches Count"),
            (ProfilerCategory.Render, "SetPass Calls Count"),
            (ProfilerCategory.Render, "Draw Calls Count"),
            (ProfilerCategory.Render, "Triangles Count"),
            (ProfilerCategory.Render, "Vertices Count"),
            (ProfilerCategory.Render, "Shadow Casters Count"),
            (ProfilerCategory.Memory, "Total Used Memory"),
            (ProfilerCategory.Memory, "Total Reserved Memory"),
            (ProfilerCategory.Memory, "GC Used Memory"),
            (ProfilerCategory.Memory, "GC Reserved Memory"),
            (ProfilerCategory.Memory, "Gfx Used Memory"),
            (ProfilerCategory.Memory, "System Used Memory"),
            (ProfilerCategory.Memory, "GC Allocated In Frame"),
            (ProfilerCategory.Memory, "GC Allocation In Frame Count"),
            (ProfilerCategory.Physics, "Active Dynamic Bodies"),
            (ProfilerCategory.Physics, "Active Kinematic Bodies"),
            (ProfilerCategory.Physics, "Dynamic Bodies"),
            (ProfilerCategory.Physics, "Contacts")
        };

        private readonly float[] history = new float[HistoryLength];
        private readonly float[] sorted = new float[HistoryLength];
        private readonly ProfilerRecorder[] counters = new ProfilerRecorder[counterSources.Length];
        private readonly FrameTiming[] timings = new FrameTiming[1];
        private int head;
        private int count;

        public DebugFrameStats()
        {
            for (int i = 0; i < counterSources.Length; ++i)
                counters[i] = ProfilerRecorder.StartNew(counterSources[i].Category, counterSources[i].Name, i == 0 ? CounterSamples : 1);
        }

        /// <summary>Frames per second over the last second.</summary>
        public float Fps { get; private set; }

        /// <summary>Mean frame time over the last second, in milliseconds.</summary>
        public float AverageMs { get; private set; }

        public float MinMs { get; private set; }
        public float MaxMs { get; private set; }

        /// <summary>Frame rate of the slowest 1% of the frames kept: the stutter players feel.</summary>
        public float OnePercentLowFps { get; private set; }

        /// <summary>CPU main-thread time per frame, in milliseconds, or 0 when unknown.</summary>
        public float MainThreadMs { get; private set; }

        /// <summary>CPU render-thread time per frame, in milliseconds, or 0 when unknown.</summary>
        public float RenderThreadMs { get; private set; }

        /// <summary>GPU time per frame, in milliseconds, or 0 when the platform does not report it.</summary>
        public float GpuMs { get; private set; }

        /// <summary>Frame times kept, oldest first through <see cref="HistoryAt"/>.</summary>
        public int HistoryCount => count;

        /// <summary>A kept frame time, in milliseconds. 0 is the oldest.</summary>
        public float HistoryAt(int index)
        {
            return history[(head - count + index + HistoryLength) % HistoryLength];
        }

        /// <summary>Whether Unity provides the counter here.</summary>
        public bool Has(DebugCounter counter)
        {
            return counters[(int)counter].Valid;
        }

        /// <summary>The counter's latest value: a count, or bytes for memory. 0 when missing.</summary>
        public long Value(DebugCounter counter)
        {
            ProfilerRecorder recorder = counters[(int)counter];
            return recorder.Valid ? recorder.LastValue : 0;
        }

        /// <summary>Records one frame. Call once per frame with the unscaled delta time.</summary>
        public void Sample(float unscaledDeltaTime)
        {
            history[head] = unscaledDeltaTime * 1000f;
            head = (head + 1) % HistoryLength;
            count = Mathf.Min(count + 1, HistoryLength);
        }

        /// <summary>Works out the summary figures from the frames kept. Call a few times a second.</summary>
        public void Refresh()
        {
            if (count == 0)
                return;

            // The last second of frames: the window players read a frame rate over.
            float total = 0f;
            float min = float.MaxValue;
            float max = 0f;
            int frames = 0;
            for (int i = count - 1; i >= 0 && total < 1000f; --i)
            {
                float ms = HistoryAt(i);
                total += ms;
                min = Mathf.Min(min, ms);
                max = Mathf.Max(max, ms);
                frames++;
            }

            AverageMs = total / frames;
            MinMs = min;
            MaxMs = max;
            Fps = total > 0f ? frames * 1000f / total : 0f;

            for (int i = 0; i < count; ++i)
                sorted[i] = HistoryAt(i);
            Array.Sort(sorted, 0, count);
            int slowest = Mathf.Max(1, count / 100);
            float slowTotal = 0f;
            for (int i = count - slowest; i < count; ++i)
                slowTotal += sorted[i];
            OnePercentLowFps = slowTotal > 0f ? slowest * 1000f / slowTotal : 0f;

            MainThreadMs = AverageNanoseconds(counters[(int)DebugCounter.MainThread]) * 1e-6f;
            ReadFrameTimings();
        }

        public void Dispose()
        {
            for (int i = 0; i < counters.Length; ++i)
                counters[i].Dispose();
        }

        private void ReadFrameTimings()
        {
            RenderThreadMs = 0f;
            GpuMs = 0f;
            if (!FrameTimingManager.IsFeatureEnabled())
                return;

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) == 0)
                return;

            RenderThreadMs = (float)timings[0].cpuRenderThreadFrameTime;
            GpuMs = (float)timings[0].gpuFrameTime;
            if (MainThreadMs <= 0f)
                MainThreadMs = (float)timings[0].cpuMainThreadFrameTime;
        }

        private static float AverageNanoseconds(ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count == 0)
                return 0f;

            double sum = 0.0;
            int samples = recorder.Count;
            for (int i = 0; i < samples; ++i)
                sum += recorder.GetSample(i).Value;

            return (float)(sum / samples);
        }
    }
}
#endif
