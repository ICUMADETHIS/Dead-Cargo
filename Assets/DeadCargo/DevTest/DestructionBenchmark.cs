using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DeadCargo.Destruction;
using DeadCargo.Destruction.Voxels;
using Unity.Profiling;
using UnityEngine;

namespace DeadCargo.DevTesting
{
    /// <summary>
    /// Measures the destruction system under a heavy, game-like load and reports to the Console. Builds a field of eight
    /// asteroids, 30 to 70 m, ahead of the camera, and one more off to the side, near enough that its trail rocks turn
    /// solid. Once the field is built, the system has warmed up and the trails have grown, it fires a steady 800 hits a
    /// second at the rocks for ten seconds, the odd one a missile, and cracks a chunk off a rock every second. The load
    /// is the same at any frame rate, and every choice comes from one seed. It records frame time, GPU time, garbage and
    /// the destruction work on the main thread, stage by stage, as averages and percentiles, and breaks the worst frame
    /// down. Started from the God Menu Destruction tab; the field is left in place to look at.
    /// </summary>
    public sealed class DestructionBenchmark : MonoBehaviour
    {
        private const string Title = "Destruction benchmark";
        private const int RockCount = 8;
        private const float MinDiameter = 30f;
        private const float MaxDiameter = 70f;
        private const float FieldDistance = 320f;
        private const float FieldSpread = 180f;
        private const float NearDiameter = 40f;
        private const float NearAhead = 90f;
        private const float NearSide = 90f;
        private const float BuildTimeout = 30f;
        private const float WarmUpTimeout = 10f;
        private const float SettleSeconds = 3f;
        private const float RunSeconds = 10f;
        private const float HitsPerSecond = 800f;
        private const int MaxHitsPerFrame = 64;
        private const float CrackInterval = 1f;
        private const float GunDamage = 100f;
        private const float MissileDamage = 2500f;
        private const float MissileShare = 0.05f;
        private const int Seed = 20261116;
        private const int MaxFrames = 20000;
        private const float BytesPerKilobyte = 1024f;

        private readonly struct Percentiles
        {
            public readonly float Average;
            public readonly float Median;
            public readonly float P95;
            public readonly float P99;
            public readonly float Worst;

            public Percentiles(float average, float median, float p95, float p99, float worst)
            {
                Average = average;
                Median = median;
                P95 = p95;
                P99 = p99;
                Worst = worst;
            }
        }

        private readonly List<VoxelBody> field = new List<VoxelBody>();
        private readonly FrameTiming[] timing = new FrameTiming[1];
        private AsteroidDefinition type;
        private System.Random random;
        private UnityEngine.Random.State savedRandom;
        private bool seededEngine;
        private ProfilerRecorder garbage;
        private bool isRecordingGarbage;

        // One sample per measured frame, preallocated so the run itself allocates nothing.
        private float[] frameTimes;
        private float[] gpuTimes;
        private float[] destructionTimes;
        private float[] trailTimes;
        private float[] chipTimes;
        private float[][] stageTimes;

        public static bool IsRunning { get; private set; }

        /// <summary>One line describing the last run, or null before the first.</summary>
        public static string LastSummary { get; private set; }

        /// <summary>Starts a run. Returns false when one is already running, Play Mode is off or there is no type.</summary>
        public static bool Begin(AsteroidDefinition type)
        {
            if (IsRunning || !Application.isPlaying || type == null)
                return false;

            var benchmark = new GameObject(Title).AddComponent<DestructionBenchmark>();
            benchmark.type = type;
            IsRunning = true;
            LastSummary = "Running...";
            return true;
        }

        private IEnumerator Start()
        {
            yield return null;

            DestructionSystem system = DestructionSystem.Active;
            if (system == null)
            {
                Finish("no Destruction System in the scene; run Set Up Sector first", null);
                yield break;
            }

            // The engine's own randomness (crack faces, piece spin) is seeded too, and handed back afterwards.
            random = new System.Random(Seed);
            savedRandom = UnityEngine.Random.state;
            UnityEngine.Random.InitState(Seed);
            seededEngine = true;
            Allocate();

            Camera view = Camera.main;
            Transform eye = view != null ? view.transform : transform;
            for (int i = 0; i < RockCount; ++i)
            {
                Vector3 offset = eye.right * FieldOffset(random) + eye.up * FieldOffset(random) + eye.forward * (FieldOffset(random) * 0.5f);
                float diameter = Mathf.Lerp(MinDiameter, MaxDiameter, Next(random));
                field.Add(system.SpawnAsteroid(type, diameter, NextSeed(random), eye.position + eye.forward * FieldDistance + offset,
                    RandomRotation(random)));
            }

            field.Add(system.SpawnAsteroid(type, NearDiameter, NextSeed(random), eye.position + eye.forward * NearAhead + eye.right * NearSide,
                RandomRotation(random)));

            float started = Time.realtimeSinceStartup;
            while (!AllReady() && Time.realtimeSinceStartup - started < BuildTimeout)
                yield return null;

            float buildSeconds = Time.realtimeSinceStartup - started;
            if (!AllReady())
            {
                Finish($"the field was not built after {BuildTimeout:0} s", null);
                yield break;
            }

            started = Time.realtimeSinceStartup;
            while (system.IsWarmingUp && Time.realtimeSinceStartup - started < WarmUpTimeout)
                yield return null;

            string startUp = system.IsWarmingUp ? "the warm-up had not finished; the first frames pay for code running for the first time"
                : system.HasWarmedUp ? "warmed up before the run"
                : "no warm-up (Warm Up On Load is off on the Destruction System)";

            // Trails grow and the nearest rocks turn solid, and every collider is cooked, before measuring starts.
            started = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - started < SettleSeconds)
                yield return null;

            system.ResetStats();
            int stages = DestructionSystem.StageNames.Count;
            int frames = 0;
            int shots = 0;
            int hits = 0;
            int cracks = 0;
            int peakChips = 0;
            int peakSolid = 0;
            long garbageBytes = 0;
            float owed = 0f;
            float nextCrack = 0f;
            int collections = GC.CollectionCount(0);
            started = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - started < RunSeconds && frames < MaxFrames)
            {
                float elapsed = Time.realtimeSinceStartup - started;

                // A steady rate, whatever the frame rate; a long frame owes at most one frame's cap.
                owed = Mathf.Min(owed + HitsPerSecond * Time.unscaledDeltaTime, MaxHitsPerFrame);
                int fire = (int)owed;
                owed -= fire;
                for (int h = 0; h < fire; ++h)
                {
                    shots++;
                    if (Hit())
                        hits++;
                }

                if (elapsed >= nextCrack)
                {
                    nextCrack += CrackInterval;
                    if (Crack())
                        cracks++;
                }

                yield return null;

                FrameTimingManager.CaptureFrameTimings();
                gpuTimes[frames] = FrameTimingManager.GetLatestTimings(1, timing) > 0 ? (float)timing[0].gpuFrameTime : 0f;
                frameTimes[frames] = Time.unscaledDeltaTime * 1000f;
                destructionTimes[frames] = system.LastFrameMilliseconds;
                trailTimes[frames] = system.TrailMilliseconds;
                chipTimes[frames] = system.ChipMilliseconds;
                for (int stage = 0; stage < stages; ++stage)
                    stageTimes[stage][frames] = system.StageMilliseconds(stage);

                if (garbage.Valid)
                    garbageBytes += garbage.LastValue;
                peakChips = Mathf.Max(peakChips, system.ChipCount);
                peakSolid = Mathf.Max(peakSolid, system.SolidTrailRocks);
                frames++;
            }

            collections = GC.CollectionCount(0) - collections;
            Percentiles frame = Measure(frameTimes, frames);
            Percentiles work = Measure(destructionTimes, frames);
            Percentiles gpu = Measure(gpuTimes, frames);
            float fps = 1000f / Mathf.Max(frame.Average, 0.01f);

            var report = new StringBuilder();
            Line(report, "Field", $"{RockCount} '{type.DisplayName}' asteroids, {MinDiameter:0} to {MaxDiameter:0} m, about {FieldDistance:0} m ahead, " +
                                  $"and a {NearDiameter:0} m one {NearAhead:0} m ahead off to the side; built in {buildSeconds:0.00} s");
            Line(report, "Start-up", startUp);
            Line(report, "Load", $"{shots:N0} shots at {HitsPerSecond:0} a second ({MissileShare:P0} missiles), {hits:N0} landed, " +
                                 $"and {cracks} cracks, over {frames:N0} frames in {RunSeconds:0} s");
            Line(report, "Frame", $"{Describe(frame, 2)} ({fps:0} fps)");
            Line(report, "GPU", gpu.Worst > 0f ? Describe(gpu, 2)
                : "not reported here; turn on Frame Timing Stats in Player Settings, or use the Profiler's GPU module");
            Line(report, "Destruction", $"{Describe(work, 3)}, on the main thread");
            for (int stage = 0; stage < stages; ++stage)
                Line(report, "  " + DestructionSystem.StageNames[stage], Describe(Measure(stageTimes[stage], frames), 3));

            Line(report, "  Trails", $"{Describe(Measure(trailTimes, frames), 3)}; {system.TrailRockCount} rocks, up to {peakSolid} solid");
            Line(report, "  Chips", $"{Describe(Measure(chipTimes, frames), 3)}; up to {peakChips:N0} in the air, " +
                                    $"at most {system.ChipsPerSecond:0} a second");
            Line(report, "Worst frame", WorstFrame(frames, stages));
            Line(report, "Garbage", $"{garbageBytes / BytesPerKilobyte / Mathf.Max(frames, 1):0.0} KB a frame, {collections} collections " +
                                    "(the editor's own included)");
            Line(report, "Heavy cuts", $"{system.HeavyCuts} on a worker thread since the scene started");
            Line(report, "Scene", Describe(system));
            report.AppendLine("  note  The editor adds its own overhead; a build is faster. Use the Profiler's DeadCargo.* markers for detail.");
            Finish($"{frame.Average:0.0} ms a frame (p99 {frame.P99:0.0}), destruction {work.Average:0.00} ms average, " +
                   $"p99 {work.P99:0.00}, worst {work.Worst:0.00}", report.ToString());
        }

        private void Allocate()
        {
            frameTimes = new float[MaxFrames];
            gpuTimes = new float[MaxFrames];
            destructionTimes = new float[MaxFrames];
            trailTimes = new float[MaxFrames];
            chipTimes = new float[MaxFrames];
            stageTimes = new float[DestructionSystem.StageNames.Count][];
            for (int stage = 0; stage < stageTimes.Length; ++stage)
                stageTimes[stage] = new float[MaxFrames];

            garbage = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            isRecordingGarbage = true;
        }

        private bool AllReady()
        {
            foreach (VoxelBody rock in field)
            {
                if (rock == null || !rock.IsReady)
                    return false;
            }

            return true;
        }

        private VoxelBody RandomRock()
        {
            for (int attempt = 0; attempt < field.Count; ++attempt)
            {
                VoxelBody rock = field[random.Next(field.Count)];
                if (rock != null && rock.IsAllocated && !rock.IsReleased)
                    return rock;
            }

            return null;
        }

        // A shot from a random side, as a weapon would land it: a ray to the rock's surface, then a hit there.
        private bool Hit()
        {
            VoxelBody rock = RandomRock();
            if (rock == null)
                return false;

            Vector3 side = RandomDirection(random);
            bool missile = Next(random) < MissileShare;
            Vector3 centre = rock.CentreOfMass;
            float reach = rock.EquivalentDiameter + 20f;
            if (!Physics.Raycast(centre + side * reach, -side, out RaycastHit surface, reach * 2f))
                return false;

            var receiver = surface.collider.GetComponent<DestructionDamageReceiver>();
            if (receiver == null || !ReferenceEquals(receiver.Target, rock))
                return false;

            rock.ApplyDamage(new DamageEvent(missile ? MissileDamage : GunDamage, surface.point, -side,
                missile ? DamageKind.Explosion : DamageKind.Weapon));
            return true;
        }

        private bool Crack()
        {
            VoxelBody rock = RandomRock();
            Vector3 direction = RandomDirection(random);
            Vector2 range = VoxelBody.FractureShareRange((FractureSize)random.Next(0, 3));
            float share = Mathf.Lerp(range.x, range.y, Next(random));
            return rock != null && rock.Fracture(direction, share, DamageKind.Script);
        }

        // The frame with the most destruction work, stage by stage, and how far into the run it came.
        private string WorstFrame(int frames, int stages)
        {
            if (frames == 0)
                return "no frames measured";

            int worst = 0;
            for (int i = 1; i < frames; ++i)
            {
                if (destructionTimes[i] > destructionTimes[worst])
                    worst = i;
            }

            var text = new StringBuilder();
            text.Append($"{destructionTimes[worst]:0.00} ms of destruction in a {frameTimes[worst]:0.00} ms frame, {worst + 1} frames in:");
            for (int stage = 0; stage < stages; ++stage)
                text.Append($" {DestructionSystem.StageNames[stage]} {stageTimes[stage][worst]:0.00},");

            text.Append($" Trails {trailTimes[worst]:0.00}, Chips {chipTimes[worst]:0.00}");
            return text.ToString();
        }

        private static string Describe(in Percentiles spread, int decimals)
        {
            string format = decimals == 3 ? "0.000" : "0.00";
            return $"{spread.Average.ToString(format)} ms average, median {spread.Median.ToString(format)}, " +
                   $"p95 {spread.P95.ToString(format)}, p99 {spread.P99.ToString(format)}, worst {spread.Worst:0.00}";
        }

        private static string Describe(DestructionSystem system)
        {
            int rocks = 0;
            int pieces = 0;
            int half = 0;
            int quarter = 0;
            int triangles = 0;
            foreach (VoxelBody body in system.Bodies)
            {
                if (body == null || !body.IsAllocated || body.IsWarmUp)
                    continue;

                if (body.IsPiece)
                    pieces++;
                else
                    rocks++;

                if (body.DetailLevel == 1)
                    half++;
                else if (body.DetailLevel >= 2)
                    quarter++;

                triangles += body.TriangleCount;
            }

            int full = rocks + pieces - half - quarter;
            return $"{rocks} rocks and {pieces} pieces; detail {full} full, {half} half, {quarter} quarter; {triangles:N0} triangles, " +
                   $"{system.ChipCount:N0} chips";
        }

        // Average and percentiles of the first count samples.
        private static Percentiles Measure(float[] samples, int count)
        {
            if (count <= 0)
                return default;

            var sorted = new float[count];
            Array.Copy(samples, sorted, count);
            Array.Sort(sorted);
            double sum = 0.0;
            foreach (float sample in sorted)
                sum += sample;

            return new Percentiles((float)(sum / count), At(sorted, 0.5f), At(sorted, 0.95f), At(sorted, 0.99f), sorted[count - 1]);
        }

        private static float At(float[] sorted, float share)
        {
            return sorted[Mathf.Clamp(Mathf.CeilToInt(share * sorted.Length) - 1, 0, sorted.Length - 1)];
        }

        private static float Next(System.Random random)
        {
            return (float)random.NextDouble();
        }

        private static uint NextSeed(System.Random random)
        {
            return (uint)random.Next(1, int.MaxValue);
        }

        private static float FieldOffset(System.Random random)
        {
            return (Next(random) * 2f - 1f) * FieldSpread;
        }

        private static Vector3 RandomDirection(System.Random random)
        {
            float z = Next(random) * 2f - 1f;
            float angle = Next(random) * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(r * Mathf.Cos(angle), r * Mathf.Sin(angle), z);
        }

        // Uniform over every orientation (Shoemake's method).
        private static Quaternion RandomRotation(System.Random random)
        {
            float u1 = Next(random);
            float u2 = Next(random) * Mathf.PI * 2f;
            float u3 = Next(random) * Mathf.PI * 2f;
            float a = Mathf.Sqrt(1f - u1);
            float b = Mathf.Sqrt(u1);
            return new Quaternion(a * Mathf.Sin(u2), a * Mathf.Cos(u2), b * Mathf.Sin(u3), b * Mathf.Cos(u3));
        }

        private static void Line(StringBuilder report, string name, string detail)
        {
            report.Append("  ").Append(name).Append("  ·  ").AppendLine(detail);
        }

        private void Finish(string summary, string report)
        {
            LastSummary = summary;
            if (report != null)
                Debug.Log($"{Title}: {summary}.\n{report}");
            else
                Debug.LogWarning($"{Title}: {summary}.");

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            IsRunning = false;
            if (isRecordingGarbage)
                garbage.Dispose();
            if (seededEngine)
                UnityEngine.Random.state = savedRandom;

            if (LastSummary == "Running...")
                LastSummary = "Stopped before it finished";
        }
    }
}
