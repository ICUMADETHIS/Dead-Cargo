using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// Runs every voxel body's work as batched jobs, once a frame, in a fixed order:
    /// <list type="number">
    /// <item>Finished collider cooking goes live, a budget per frame.</item>
    /// <item>A finished mesh batch is applied; its colliders start cooking at the end of this frame.</item>
    /// <item>Finished generation queues the new rock's chunks.</item>
    /// <item>Finished piece moves become free bodies and chips; a finished piece search starts moving its pieces out.</item>
    /// <item>Released bodies are freed; one a job still uses stays hidden until the job is done.</item>
    /// <item>A finished heavy cut is applied. Queued cuts are applied: craters inline, a cut across a whole rock on a
    /// worker. Their chunks are queued urgently and a piece search is queued.</item>
    /// <item>Twice a second, rock switches mesh detail by distance and far pieces are removed.</item>
    /// <item>A piece search, a mesh batch and collider cooking are scheduled, each when the last one is done.</item>
    /// </list>
    /// The main thread never waits for a job: work still running is picked up a frame later. Instead, nothing writes a
    /// grid while a job reads it: a body being meshed, searched, emptied of pieces or cut on a worker takes its cuts a
    /// frame later, so grids need no locks. Cuts usually appear one frame after the hit, and colliders a frame or two
    /// after that. Only rebuilding or destroying a body waits, and only for the jobs that use that body.
    /// </summary>
    internal sealed class VoxelScheduler : IDisposable
    {
        /// <summary>Vertices one chunk may hold. Far beyond any rock surface; a chunk that needs more is cut short and reported.</summary>
        public const int ChunkVertexCapacity = 16384;

        public const int ChunkIndexCapacity = 6 * ChunkVertexCapacity;

        private const byte NotQueued = 0;
        private const byte QueuedBackground = 1;
        private const byte QueuedUrgent = 2;

        private const int IslandCapacity = 32;
        private const float ReviewInterval = 0.5f;

        // A crater wider than this, in voxels, is cut on a worker like a crack. Smaller ones cost well under 0.1 ms inline.
        private const float HeavyCraterVoxels = 24f;

        // Voxel size the half-detail distance is set for; rock with larger voxels switches proportionally further out.
        private const float ReferenceVoxelSize = 0.5f;

        // Share of the half-detail distance either side of it before rock switches back, so it never flickers.
        private const float DetailHysteresis = 0.1f;

        // Rock narrower than these, in voxels, never meshes at half or quarter detail: the coarser grid could miss it.
        private const float MinHalfDetailVoxels = 16f;
        private const float MinQuarterDetailVoxels = 32f;

        private const string DitherFadeKeyword = "_DITHER_FADE";

        private readonly struct FadeRef
        {
            public readonly VoxelBody Body;
            public readonly VoxelChunk Chunk;

            public FadeRef(VoxelBody body, VoxelChunk chunk)
            {
                Body = body;
                Chunk = chunk;
            }
        }

        // A piece this share of its parent's starting size flies at the type's split speed; smaller ones go faster.
        private const float ReferencePieceShare = 0.05f;

        private readonly DestructionSystem system;
        private readonly List<VoxelBody> bodies = new List<VoxelBody>();
        private readonly List<VoxelBody> snapshot = new List<VoxelBody>();
        private readonly List<ChunkRef> urgent = new List<ChunkRef>();
        private readonly List<ChunkRef> background = new List<ChunkRef>();
        private readonly List<VoxelBody> islandQueue = new List<VoxelBody>();
        private readonly List<VoxelBody> releases = new List<VoxelBody>();
        private readonly CarveBatch inlineCarves = new CarveBatch();
        private readonly CarveBatch heavyCarves = new CarveBatch();
        private readonly Stack<Mesh> meshPool = new Stack<Mesh>();
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();

        private NativeArray<VertexAttributeDescriptor> layout;

        private readonly List<ChunkRef> batch = new List<ChunkRef>();
        private readonly List<Mesh> batchMeshes = new List<Mesh>();
        private NativeArray<sbyte> blocks;
        private NativeArray<byte> blockMaterials;
        private NativeArray<int> cellVertices;
        private NativeArray<VoxelVertex> vertices;
        private NativeArray<ushort> indices;
        private NativeArray<MeshRequest> requests;
        private NativeArray<MeshResult> results;
        private int meshSlots;
        private Mesh.MeshDataArray meshData;
        private JobHandle meshHandle;
        private bool meshInFlight;

        private readonly List<VoxelChunk> pendingBakeChunks = new List<VoxelChunk>();
        private readonly List<Mesh> pendingBakeMeshes = new List<Mesh>();
        private readonly List<VoxelChunk> bakingChunks = new List<VoxelChunk>();
        private readonly List<Mesh> bakingMeshes = new List<Mesh>();
        private NativeArray<EntityId> bakeIds;
        private JobHandle bakeHandle;
        private bool bakeInFlight;

        private NativeArray<ushort> labels;
        private NativeArray<int> queue;
        private NativeArray<int> islandVoxels;
        private NativeArray<int> parent;
        private NativeArray<int> unionSize;
        private NativeArray<int> frontier;
        private NativeArray<IslandInfo> islands;
        private NativeArray<int3> islandSummary;
        private VoxelBody islandBody;
        private int islandVersion;
        private JobHandle islandHandle;
        private bool islandInFlight;

        // Pieces a search found are moved out of their body by a job; the body and its new pieces wait until it is done.
        private readonly List<VoxelBody> extractionPieces = new List<VoxelBody>();
        private NativeArray<ExtractTarget> extractTargets;
        private NativeArray<float> extractVolumes;
        private NativeArray<MassMoments> extractMoments;
        private VoxelBody extractionBody;
        private int extractionVersion;
        private int extractionCount;
        private JobHandle extractionHandle;
        private bool extractionInFlight;

        private readonly HashSet<Mesh> meshesInBake = new HashSet<Mesh>();
        private readonly List<Mesh> releaseAfterBake = new List<Mesh>();

        // Detail cross-fades running, the outgoing meshes their ghosts show, and those meshes asked back meanwhile.
        private readonly List<FadeRef> fading = new List<FadeRef>();
        private readonly HashSet<Mesh> meshesInFade = new HashSet<Mesh>();
        private readonly HashSet<Mesh> releaseAfterFade = new HashSet<Mesh>();
        private readonly Dictionary<Material, Material> fadeMaterials = new Dictionary<Material, Material>();
        private readonly List<VoxelChunk> readyColliderChunks = new List<VoxelChunk>();
        private readonly List<Mesh> readyColliderMeshes = new List<Mesh>();
        private readonly List<ChunkRef> deferred = new List<ChunkRef>();

        private float nextReview;
        private int carveCursor;
        private bool overflowReported;
        private bool truncationReported;

        public VoxelScheduler(DestructionSystem system)
        {
            this.system = system;
            layout = VoxelMeshJob.CreateLayout(Allocator.Persistent);
            islandSummary = new NativeArray<int3>(1, Allocator.Persistent);
            islands = new NativeArray<IslandInfo>(IslandCapacity, Allocator.Persistent);
            parent = new NativeArray<int>(IslandBuffers.MaxSeeds + 1, Allocator.Persistent);
            unionSize = new NativeArray<int>(IslandBuffers.MaxSeeds + 1, Allocator.Persistent);
            frontier = new NativeArray<int>(IslandBuffers.MaxSeeds + 1, Allocator.Persistent);
            extractTargets = new NativeArray<ExtractTarget>(IslandCapacity, Allocator.Persistent);
            extractVolumes = new NativeArray<float>(IslandCapacity * VoxelEncoding.MaterialSlots, Allocator.Persistent);
            extractMoments = new NativeArray<MassMoments>(IslandCapacity, Allocator.Persistent);
        }

        public IReadOnlyList<VoxelBody> Bodies => bodies;
        public int QueuedChunks => urgent.Count + background.Count;
        public int MeshedLastFrame { get; private set; }
        public int PieceSearches { get; private set; }
        public int LastSearchVisited { get; private set; }

        /// <summary>Cuts across whole rocks made on a worker since the start.</summary>
        public int HeavyCuts { get; private set; }

        /// <summary>The stages of a frame's work, as timed by <see cref="StageMilliseconds"/>.</summary>
        public static readonly string[] StageNames = { "Colliders", "Meshes", "Pieces", "Cuts", "Scheduling" };

        private const int StageColliders = 0;
        private const int StageMeshes = 1;
        private const int StagePieces = 2;
        private const int StageCuts = 3;
        private const int StageScheduling = 4;

        private static readonly ProfilerMarker CollidersMarker = new ProfilerMarker("DeadCargo.Voxels.Colliders");
        private static readonly ProfilerMarker MeshesMarker = new ProfilerMarker("DeadCargo.Voxels.ApplyMeshes");
        private static readonly ProfilerMarker PiecesMarker = new ProfilerMarker("DeadCargo.Voxels.Pieces");
        private static readonly ProfilerMarker CutsMarker = new ProfilerMarker("DeadCargo.Voxels.Cuts");
        private static readonly ProfilerMarker ScheduleMarker = new ProfilerMarker("DeadCargo.Voxels.Schedule");

        private readonly float[] stageMilliseconds = new float[5];
        private readonly float[] peakStageMilliseconds = new float[5];

        /// <summary>Memory held by the shared job buffers, in bytes.</summary>
        public long ScratchBytes =>
            (long)meshSlots * (SurfaceNets.BlockLength * 2L + SurfaceNets.CellMapLength * 4L + ChunkVertexCapacity * 20L + ChunkIndexCapacity * 2L) +
            (labels.IsCreated ? labels.Length * 10L : 0L);

        public void Register(VoxelBody body)
        {
            if (!bodies.Contains(body))
                bodies.Add(body);
        }

        public void Unregister(VoxelBody body)
        {
            bodies.Remove(body);
        }

        /// <summary>
        /// Finishes the running jobs that read or write this body's grid, so it can be rebuilt or freed. Other bodies'
        /// work carries on. Results are still applied by the next <see cref="Tick"/>.
        /// </summary>
        public void CompleteJobsFor(VoxelBody body)
        {
            body.Generation.Complete();
            body.GridJob.Complete();
            if (body.InMeshBatch)
                meshHandle.Complete();
            if (islandInFlight && body == islandBody)
                islandHandle.Complete();
        }

        /// <summary>Adds empty meshes to the pool, so a burst of cuts later never has to make them mid-frame.</summary>
        public void PrewarmMeshes(int count)
        {
            for (int i = meshPool.Count; i < count; ++i)
                meshPool.Push(CreateMesh());
        }

        /// <summary>Makes a rock material's cross-fade copy now, rather than at its first detail switch.</summary>
        public void PrewarmFade(Material source)
        {
            FadeMaterial(source);
        }

        /// <summary>Finishes every running job. Results are still applied by the next <see cref="Tick"/>.</summary>
        public void CompleteJobs()
        {
            meshHandle.Complete();
            bakeHandle.Complete();
            islandHandle.Complete();
            extractionHandle.Complete();
            heavyCarves.Handle.Complete();
            foreach (VoxelBody body in bodies)
            {
                if (body != null)
                    body.Generation.Complete();
            }
        }

        public void Tick()
        {
            long time = Stopwatch.GetTimestamp();
            using (CollidersMarker.Auto())
                CompleteBakes();
            Record(StageColliders, ref time);

            using (MeshesMarker.Auto())
            {
                CompleteMeshes();
                UpdateFades();
            }
            Record(StageMeshes, ref time);

            using (PiecesMarker.Auto())
            {
                CompleteGeneration();
                FinishExtraction();
                StartExtraction();
                ProcessReleases();
            }

            Record(StagePieces, ref time);

            using (CutsMarker.Auto())
                ProcessCarves();
            Record(StageCuts, ref time);

            using (ScheduleMarker.Auto())
            {
                ReviewDistances();
                ScheduleIslands();
                ScheduleMeshes();
                ScheduleBakes();
                JobHandle.ScheduleBatchedJobs();
            }

            Record(StageScheduling, ref time);
        }

        /// <summary>Main-thread time of a stage in the last frame, in milliseconds. See <see cref="StageNames"/>.</summary>
        public float StageMilliseconds(int stage)
        {
            return stageMilliseconds[stage];
        }

        /// <summary>Worst main-thread time of a stage since <see cref="ResetPeaks"/>, in milliseconds.</summary>
        public float PeakStageMilliseconds(int stage)
        {
            return peakStageMilliseconds[stage];
        }

        public void ResetPeaks()
        {
            Array.Clear(peakStageMilliseconds, 0, peakStageMilliseconds.Length);
        }

        private void Record(int stage, ref long time)
        {
            long now = Stopwatch.GetTimestamp();
            float milliseconds = (float)((now - time) * 1000.0 / Stopwatch.Frequency);
            stageMilliseconds[stage] = milliseconds;
            peakStageMilliseconds[stage] = Mathf.Max(peakStageMilliseconds[stage], milliseconds);
            time = now;
        }

        /// <summary>Moves every rock along its drift and tumble. Runs in the physics step.</summary>
        public void MoveBodies(float deltaTime)
        {
            foreach (VoxelBody body in bodies)
            {
                if (body != null && body.IsAllocated && !body.ReleaseRequested)
                    body.Integrate(deltaTime);
            }
        }

        /// <summary>Starts building an asteroid in a body. Its chunks are meshed once the rock is filled in.</summary>
        public void Generate(VoxelBody body, AsteroidDefinition definition, float diameter, uint seed)
        {
            // Only a body being rebuilt can have jobs running on it; a new one starts at once.
            CompleteJobsFor(body);
            ClearChunks(body);

            AsteroidShape shape = definition.BuildShape(diameter, seed);
            float voxelSize = definition.VoxelSize;
            int maxHalf = (VoxelBody.MaxGridSize - 1) / 2;
            int half = (int)math.ceil(shape.MaxRadius / voxelSize) + VoxelBody.Margin;
            if (half > maxHalf)
            {
                voxelSize = shape.MaxRadius / (maxHalf - VoxelBody.Margin);
                half = maxHalf;
            }

            var size = new int3(half * 2 + 1);
            body.Allocate(definition, size, voxelSize, new float3(-half * voxelSize));
            body.SliceMoments = new NativeArray<MassMoments>(size.z, Allocator.Persistent);

            // The spin waits for the rock's inertia, known once it is filled in.
            definition.RollMotion(diameter, seed, out Vector3 drift, out Vector3 tumble);
            body.SetMotion(drift, tumble);

            var job = new VoxelFillJob
            {
                Density = body.Density,
                Material = body.Material,
                Size = size,
                Shape = shape,
                VoxelSize = voxelSize,
                Origin = body.GridOrigin,
                SliceMoments = body.SliceMoments
            };

            body.Generation = job.Schedule(size.z, 1);
            body.IsGenerating = true;
            JobHandle.ScheduleBatchedJobs();

            // Searching this rock for pieces needs buffers its size; make them now, while a new rock is expected to
            // cost something, rather than at its first break.
            if (!islandInFlight && !extractionInFlight)
                EnsureIslandCapacity(body.Density.Length);
        }

        public void QueueRelease(VoxelBody body)
        {
            if (body == null || body.ReleaseRequested)
                return;

            body.ReleaseRequested = true;
            body.SetVisible(false);
            releases.Add(body);
        }

        public void Dispose()
        {
            CompleteJobs();
            if (meshInFlight)
            {
                meshData.Dispose();
                meshInFlight = false;
            }

            DisposeArray(ref layout);
            DisposeArray(ref blocks);
            DisposeArray(ref blockMaterials);
            DisposeArray(ref cellVertices);
            DisposeArray(ref vertices);
            DisposeArray(ref indices);
            DisposeArray(ref requests);
            DisposeArray(ref results);
            DisposeArray(ref bakeIds);
            DisposeArray(ref labels);
            DisposeArray(ref queue);
            DisposeArray(ref islandVoxels);
            DisposeArray(ref parent);
            DisposeArray(ref unionSize);
            DisposeArray(ref frontier);
            DisposeArray(ref islands);
            DisposeArray(ref islandSummary);
            inlineCarves.Dispose();
            heavyCarves.Dispose();
            DisposeArray(ref extractTargets);
            DisposeArray(ref extractVolumes);
            DisposeArray(ref extractMoments);

            foreach (Material fade in fadeMaterials.Values)
            {
                if (fade != null)
                    UnityEngine.Object.Destroy(fade);
            }

            fadeMaterials.Clear();

            foreach (Mesh mesh in ownedMeshes)
            {
                if (mesh != null)
                    UnityEngine.Object.Destroy(mesh);
            }

            ownedMeshes.Clear();
            meshPool.Clear();
        }

        // Cooked colliders go live, a budget per frame; the rest wait a frame, still colliding with their previous shape.
        // Cooking that has not finished is picked up next frame: the main thread never waits for it.
        private void CompleteBakes()
        {
            if (bakeInFlight && bakeHandle.IsCompleted)
            {
                bakeHandle.Complete();
                bakeInFlight = false;
                readyColliderChunks.AddRange(bakingChunks);
                readyColliderMeshes.AddRange(bakingMeshes);
                bakingChunks.Clear();
                bakingMeshes.Clear();
                meshesInBake.Clear();

                foreach (Mesh mesh in releaseAfterBake)
                    ReleaseMesh(mesh);
                releaseAfterBake.Clear();
            }

            int assigned = 0;
            int i = 0;
            for (; i < readyColliderChunks.Count && assigned < system.CollidersPerFrame; ++i)
            {
                VoxelChunk chunk = readyColliderChunks[i];
                Mesh mesh = readyColliderMeshes[i];
                if (!chunk.IsAlive || chunk.RenderMesh != mesh)
                    continue;

                if (chunk.ColliderMesh != null && chunk.ColliderMesh != mesh)
                    ReleaseMesh(chunk.ColliderMesh);

                // Clearing first makes the collider pick up the new shape from the cooked data.
                chunk.ColliderMesh = mesh;
                chunk.Collider.sharedMesh = null;
                chunk.Collider.sharedMesh = mesh;
                chunk.Collider.enabled = true;
                assigned++;
            }

            readyColliderChunks.RemoveRange(0, i);
            readyColliderMeshes.RemoveRange(0, i);
        }

        // A batch still meshing is applied next frame: the main thread never waits for it.
        private void CompleteMeshes()
        {
            MeshedLastFrame = 0;
            if (!meshInFlight || !meshHandle.IsCompleted)
                return;

            meshHandle.Complete();
            meshInFlight = false;
            Mesh.ApplyAndDisposeWritableMeshData(meshData, batchMeshes, VoxelMeshJob.UpdateFlags);
            MeshedLastFrame = batch.Count;
            foreach (ChunkRef entry in batch)
            {
                if (entry.Body != null)
                    entry.Body.InMeshBatch = false;
            }

            snapshot.Clear();
            for (int i = 0; i < batch.Count; ++i)
            {
                ChunkRef entry = batch[i];
                Mesh mesh = batchMeshes[i];
                if (!entry.IsCurrent)
                {
                    ReleaseMesh(mesh);
                    continue;
                }

                VoxelBody body = entry.Body;
                body.PendingMeshes--;
                MeshResult result = results[i];
                if (result.Overflow && !overflowReported)
                {
                    overflowReported = true;
                    Debug.LogWarning($"A chunk of '{body.name}' needed more than {ChunkVertexCapacity} vertices and was cut short. " +
                                     "Use larger voxels for this asteroid type.", body);
                }

                ApplyChunk(body, entry.Index, mesh, result, requests[i].Stride);
                if (!snapshot.Contains(body))
                    snapshot.Add(body);
            }

            batch.Clear();
            batchMeshes.Clear();

            foreach (VoxelBody body in snapshot)
            {
                if (body.PendingMeshes > 0 || body.IsGenerating || body.IsBusy)
                    continue;

                body.HasBeenMeshed = true;
                if (body.SurfaceChunks == 0 && body.PendingCarves.Count == 0)
                {
                    // Reduced detail can step over a sliver; only an empty mesh at full detail means the rock is gone.
                    if (body.DetailLevel > 0)
                    {
                        body.SetDetailLevel(0);
                        for (int i = 0; i < body.Chunks.Length; ++i)
                            Enqueue(body, i, true);
                        continue;
                    }

                    body.RaiseDepleted();
                    QueueRelease(body);
                }
            }
        }

        /// <param name="stride">Detail the mesh was built at: 1 full, 2 half, 4 quarter.</param>
        private void ApplyChunk(VoxelBody body, int index, Mesh mesh, in MeshResult result, int stride)
        {
            if (result.IndexCount == 0)
            {
                ReleaseMesh(mesh);
                ClearChunk(body, index);
                return;
            }

            VoxelChunk chunk = body.GetOrCreateChunk(index);
            mesh.bounds = new Bounds((result.BoundsMin + result.BoundsMax) * 0.5f, result.BoundsMax - result.BoundsMin);

            // A new detail level cross-fades in over the old mesh; a cut shows at once.
            Mesh old = chunk.RenderMesh;
            bool crossFade = old != null && chunk.MeshStride != stride && system.DetailFadeTime > 0f;
            chunk.RenderMesh = mesh;
            chunk.Filter.sharedMesh = mesh;
            chunk.Renderer.enabled = true;
            chunk.MeshStride = stride;
            if (crossFade)
                StartFade(body, chunk, old);
            else if (old != null && old != chunk.ColliderMesh)
                ReleaseMesh(old);

            int triangles = result.IndexCount / 3;
            if (chunk.Triangles == 0)
                body.SurfaceChunks++;

            body.TriangleCount += triangles - chunk.Triangles;
            chunk.Triangles = triangles;

            pendingBakeChunks.Add(chunk);
            pendingBakeMeshes.Add(mesh);
        }

        // Shows the outgoing mesh on the chunk's ghost and fades the new one in over it. A fade still running hands its
        // own outgoing mesh back first.
        private void StartFade(VoxelBody body, VoxelChunk chunk, Mesh outgoing)
        {
            if (chunk.IsFading)
                FinishFadeMesh(chunk, chunk.EndFade());

            Material fade = FadeMaterial(body.Definition != null ? body.Definition.SurfaceMaterial : null);
            if (fade == null)
            {
                if (outgoing != chunk.ColliderMesh)
                    ReleaseMesh(outgoing);
                return;
            }

            chunk.BeginFade(outgoing, fade, Time.time);
            meshesInFade.Add(outgoing);
            fading.Add(new FadeRef(body, chunk));
            body.ApplyTextureOffset(chunk);
        }

        // Moves every cross-fade on, and ends those that are done.
        private void UpdateFades()
        {
            float duration = system.DetailFadeTime;
            float now = Time.time;
            for (int i = fading.Count - 1; i >= 0; --i)
            {
                FadeRef entry = fading[i];
                VoxelChunk chunk = entry.Chunk;
                if (entry.Body == null || !chunk.IsAlive || !chunk.IsFading)
                {
                    fading.RemoveAt(i);
                    continue;
                }

                float progress = duration > 0f ? (now - chunk.FadeStart) / duration : 1f;
                if (progress >= 1f)
                {
                    FinishFadeMesh(chunk, chunk.EndFade());
                    fading.RemoveAt(i);
                }
                else
                {
                    chunk.FadeProgress = progress;
                }

                entry.Body.ApplyTextureOffset(chunk);
            }
        }

        // The outgoing mesh of a finished fade goes back to the pool, unless the collider still uses it: the collider
        // hands it back once its new shape is cooked.
        private void FinishFadeMesh(VoxelChunk chunk, Mesh outgoing)
        {
            if (outgoing == null)
                return;

            meshesInFade.Remove(outgoing);
            bool asked = releaseAfterFade.Remove(outgoing);
            if (asked || outgoing != chunk.ColliderMesh && outgoing != chunk.RenderMesh)
                ReleaseMesh(outgoing);
        }

        // The rock's material with the dither fade on, made once per material.
        private Material FadeMaterial(Material source)
        {
            if (source == null)
                return null;

            if (fadeMaterials.TryGetValue(source, out Material fade))
                return fade;

            fade = new Material(source) { name = source.name + " (Detail Fade)" };
            fade.EnableKeyword(DitherFadeKeyword);
            fadeMaterials.Add(source, fade);
            return fade;
        }

        private void ClearChunk(VoxelBody body, int index)
        {
            VoxelChunk chunk = body.Chunks[index];
            if (chunk == null)
                return;

            if (chunk.IsFading)
                FinishFadeMesh(chunk, chunk.EndFade());

            if (chunk.Triangles > 0)
                body.SurfaceChunks--;

            body.TriangleCount -= chunk.Triangles;
            chunk.Triangles = 0;

            if (chunk.IsAlive)
            {
                chunk.Filter.sharedMesh = null;
                chunk.Renderer.enabled = false;
                chunk.Collider.sharedMesh = null;
                chunk.Collider.enabled = false;
            }

            if (chunk.ColliderMesh != null && chunk.ColliderMesh != chunk.RenderMesh)
                ReleaseMesh(chunk.ColliderMesh);
            if (chunk.RenderMesh != null)
                ReleaseMesh(chunk.RenderMesh);

            chunk.RenderMesh = null;
            chunk.ColliderMesh = null;
            body.RetireChunk(index);
        }

        private void ClearChunks(VoxelBody body)
        {
            for (int i = 0; i < body.Chunks.Length; ++i)
                ClearChunk(body, i);
        }

        private void CompleteGeneration()
        {
            for (int b = 0; b < bodies.Count; ++b)
            {
                VoxelBody body = bodies[b];
                if (body == null || !body.IsGenerating || !body.Generation.IsCompleted)
                    continue;

                body.Generation.Complete();
                body.IsGenerating = false;

                // Released while it was being built: it is freed this frame, never meshed.
                if (body.ReleaseRequested)
                    continue;

                MassMoments moments = default;
                for (int z = 0; z < body.SliceMoments.Length; ++z)
                    moments += body.SliceMoments[z];
                body.SliceMoments.Dispose();

                body.Moments = moments;
                body.InitialVolume = body.Volume = (float)moments.Volume * Cube(body.VoxelSize);
                body.UpdateMassProperties();

                // Far rock is meshed straight at the detail its distance calls for, not at full detail and then again.
                if (TryGetView(out Vector3 view))
                    body.SetDetailLevel(WantedDetail(body, view));

                for (int i = 0; i < body.Chunks.Length; ++i)
                    Enqueue(body, i, false);
            }
        }

        // A finished search hands its pieces to a job that moves them out of the body. Nothing waits: a search still
        // running is picked up next frame, and the body takes no cuts until its pieces are out.
        private void StartExtraction()
        {
            // Moving pieces out writes the grid, so it also waits for any meshing of the body to finish.
            if (!islandInFlight || !islandHandle.IsCompleted || extractionInFlight || islandBody != null && islandBody.InMeshBatch)
                return;

            islandHandle.Complete();
            islandInFlight = false;
            VoxelBody body = islandBody;
            islandBody = null;
            if (body == null || !body.IsAllocated || body.ReleaseRequested || body.Version != islandVersion)
                return;

            int3 summary = islandSummary[0];
            PieceSearches++;
            LastSearchVisited = summary.y;
            if (summary.z != 0 && !truncationReported)
            {
                truncationReported = true;
                Debug.LogWarning($"A cut on '{body.name}' touched more separate surfaces than one search can follow; a piece may not " +
                                 "have broken away. Smaller cuts avoid this.", body);
            }

            if (summary.x == 0)
                return;

            const int margin = VoxelBody.Margin;
            extractionPieces.Clear();
            for (int i = 0; i < summary.x; ++i)
            {
                IslandInfo island = islands[i];
                if (island.IsOverflow || island.Count < system.MinPieceVoxels)
                {
                    extractTargets[i] = ExtractTarget.Crumble;
                    extractionPieces.Add(null);
                    continue;
                }

                int3 size = island.Max - island.Min + 1 + margin * 2;
                VoxelBody piece = system.AcquirePiece();
                piece.Allocate(body.Definition, size, body.VoxelSize, body.GridOrigin + (float3)(island.Min - margin) * body.VoxelSize);
                piece.IsBusy = true;
                extractTargets[i] = ExtractTarget.Into(piece.Density, piece.Material, size, new int3(margin));
                extractionPieces.Add(piece);
            }

            extractionHandle = new VoxelExtractJob
            {
                SourceDensity = body.Density,
                SourceMaterial = body.Material,
                SourceSize = body.GridSize,
                Voxels = islandVoxels,
                Islands = islands,
                Targets = extractTargets,
                Count = summary.x,
                MaterialVolume = extractVolumes,
                Moments = extractMoments
            }.Schedule();

            body.IsBusy = true;
            body.GridJob = extractionHandle;
            foreach (VoxelBody piece in extractionPieces)
            {
                if (piece != null)
                    piece.GridJob = extractionHandle;
            }

            extractionBody = body;
            extractionVersion = body.Version;
            extractionCount = summary.x;
            extractionInFlight = true;
        }

        private void FinishExtraction()
        {
            if (!extractionInFlight || !extractionHandle.IsCompleted)
                return;

            extractionHandle.Complete();
            extractionInFlight = false;
            VoxelBody body = extractionBody;
            extractionBody = null;
            bool valid = body != null && body.IsAllocated && !body.ReleaseRequested && body.Version == extractionVersion;
            if (body != null)
                body.IsBusy = false;

            for (int i = 0; i < extractionCount; ++i)
            {
                VoxelBody piece = extractionPieces[i];
                if (piece != null)
                    piece.IsBusy = false;

                if (!valid)
                {
                    if (piece != null)
                        QueueRelease(piece);
                    continue;
                }

                if (piece == null)
                    FinishCrumb(body, i);
                else if (!piece.ReleaseRequested)
                    FinishPiece(body, piece, i);
            }

            extractionPieces.Clear();
            EnforcePieceBudget();
        }

        private void FinishCrumb(VoxelBody body, int index)
        {
            IslandInfo island = islands[index];
            float removed = 0f;
            int dominant = 0;
            int o = index * VoxelEncoding.MaterialSlots;
            for (int m = 0; m < VoxelEncoding.MaterialSlots; ++m)
            {
                removed += extractVolumes[o + m];
                if (extractVolumes[o + m] > extractVolumes[o + dominant])
                    dominant = m;
            }

            removed *= Cube(body.VoxelSize);
            body.Volume -= removed;
            MassMoments crumb = extractMoments[index];
            body.Moments -= crumb;
            body.UpdateMassProperties();

            float3 middle = crumb.Volume > 0.0 ? (float3)crumb.Centre : (float3)(island.Min + island.Max) * 0.5f;
            Vector3 centre = body.GridToWorld(middle);
            Vector3 outward = (centre - body.CentreOfMass).normalized;
            float extent = math.cmax(island.Max - island.Min + 1) * body.VoxelSize * 0.5f;
            system.Debris.Burst(centre, outward, ChipColor(body.Definition, dominant), removed, extent);
            MarkDirty(body, island.Min, island.Max);
        }

        private void FinishPiece(VoxelBody body, VoxelBody piece, int index)
        {
            IslandInfo island = islands[index];
            AsteroidDefinition definition = body.Definition;
            MassMoments moments = extractMoments[index];

            // How the rock moved just before it lost the piece: the piece carries on with that motion.
            Vector3 bodyVelocity = body.LinearVelocity;
            Vector3 bodySpin = body.AngularVelocity;
            Vector3 bodyCentre = body.CentreOfMass;

            // The piece pivots on its own centre of mass, so it tumbles the way a real rock would.
            float3 centreInBody = body.GridOrigin + (float3)moments.Centre * body.VoxelSize;
            piece.GridOrigin -= centreInBody;
            piece.SetTextureOffset(body.TextureOffset + centreInBody);
            Vector3 worldCentre = body.LocalToWorld(centreInBody);
            piece.PlaceAt(worldCentre, body.WorldRotation);

            float pieceVolume = (float)moments.Volume * Cube(body.VoxelSize);
            piece.InitialVolume = piece.Volume = pieceVolume;
            piece.Moments = moments.Transformed(1.0, new int3(VoxelBody.Margin) - island.Min);
            piece.UpdateMassProperties();
            piece.MarkAsPiece(body.IsWarmUp);

            body.Volume -= pieceVolume;
            body.Moments -= moments;
            body.UpdateMassProperties();

            piece.SetMotion(bodyVelocity + Vector3.Cross(bodySpin, worldCentre - bodyCentre), bodySpin);

            // The two push apart along the line between their centres with equal and opposite pushes, so the lighter
            // one moves more: a chip flies off a big rock that barely stirs, and two halves part at the same speed.
            Vector3 away = worldCentre - body.CentreOfMass;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : UnityEngine.Random.onUnitSphere;

            float reference = Mathf.Max(body.InitialVolume * ReferencePieceShare, 1f);
            float sizeFactor = Mathf.Clamp(Mathf.Pow(reference / Mathf.Max(pieceVolume, 1e-3f), 1f / 3f), 0.5f, 3f);
            float speed = (definition != null ? definition.SplitSpeed : 2f) * sizeFactor * UnityEngine.Random.Range(0.8f, 1.2f);
            Vector3 push = away * (speed * piece.Mass);
            piece.AddImpulse(push, piece.CentreOfMass);
            body.AddImpulse(-push, body.CentreOfMass);

            // They twist apart the same way: equal and opposite.
            float twist = (definition != null ? definition.SplitSpin : 10f) * Mathf.Deg2Rad * sizeFactor * UnityEngine.Random.Range(0.6f, 1.2f);
            Vector3 twistMomentum = piece.AngularMomentumFor(UnityEngine.Random.onUnitSphere * twist);
            piece.AddAngularImpulse(twistMomentum);
            body.AddAngularImpulse(-twistMomentum);

            // A piece starts at the detail its own size and distance call for, so a far one is not meshed twice.
            if (TryGetView(out Vector3 view))
                piece.SetDetailLevel(WantedDetail(piece, view));

            for (int i = 0; i < piece.Chunks.Length; ++i)
                Enqueue(piece, i, true);
            MarkDirty(body, island.Min, island.Max);

            if (definition != null && definition.SplitEffect != null)
                system.Effects.Play(definition.SplitEffect, body.LastCutPoint, Quaternion.LookRotation(away));

            // The warm-up rock tells only its own listener; the rest of the game never hears of it.
            body.RaiseSplit(piece);
            if (!body.IsWarmUp)
                system.RaiseBodySplit(body, piece);
        }

        private void EnforcePieceBudget()
        {
            int free = 0;
            foreach (VoxelBody body in bodies)
            {
                if (CountsAsFreePiece(body))
                    free++;
            }

            while (free > system.MaxFreePieces)
            {
                VoxelBody smallest = null;
                foreach (VoxelBody body in bodies)
                {
                    if (CountsAsFreePiece(body) && (smallest == null || body.Volume < smallest.Volume))
                        smallest = body;
                }

                if (smallest == null)
                    break;

                Crumble(smallest);
                free--;
            }
        }

        private static bool CountsAsFreePiece(VoxelBody body)
        {
            return body != null && body.IsPiece && !body.IsWarmUp && !body.ReleaseRequested;
        }

        /// <summary>Turns a body into a burst of chips and releases it.</summary>
        public void Crumble(VoxelBody body)
        {
            float radius = Mathf.Max(1f, Mathf.Pow(body.Volume * 0.75f / Mathf.PI, 1f / 3f));
            system.Debris.Burst(body.transform.position, body.LinearVelocity.sqrMagnitude > 0.01f ? body.LinearVelocity.normalized : Vector3.up,
                ChipColor(body.Definition, 0), body.Volume, radius);
            QueueRelease(body);
        }

        // Frees released bodies. One whose grid a job still reads or writes is already hidden; it waits for the job
        // rather than the main thread waiting on it.
        private void ProcessReleases()
        {
            if (releases.Count == 0)
                return;

            int waiting = 0;
            for (int i = 0; i < releases.Count; ++i)
            {
                VoxelBody body = releases[i];
                if (body == null)
                    continue;

                if (IsGridInUse(body))
                {
                    releases[waiting++] = body;
                    continue;
                }

                // Every job on the grid has finished; completing them only releases their hold on its buffers.
                CompleteJobsFor(body);
                body.IsBusy = false;
                ClearChunks(body);
                body.DisposeGrid();
                body.Version++;
                body.PendingCarves.Clear();
                body.IslandCheckQueued = false;
                system.RetireBody(body);
            }

            releases.RemoveRange(waiting, releases.Count - waiting);
        }

        // A job is still running on the body's grid: building it, meshing it, searching it, or moving pieces or a heavy
        // cut through it. A mesh batch counts until it is applied, as its requests point straight at the grid.
        private bool IsGridInUse(VoxelBody body)
        {
            return !body.Generation.IsCompleted || !body.GridJob.IsCompleted || body.InMeshBatch ||
                   islandInFlight && body == islandBody && !islandHandle.IsCompleted;
        }

        // Applies queued cuts, oldest first, up to a budget per frame. Rocks take turns at the front of the line, so
        // under heavy fire none waits behind the others for long. Craters are cut inline, as small Burst jobs; a cut
        // across a whole rock (a crack, a saw cut, a huge blast) goes to a worker and lands next frame, so it never
        // stalls the frame.
        private void ProcessCarves()
        {
            FinishHeavyCarve();

            snapshot.Clear();
            snapshot.AddRange(bodies);
            int budget = system.CutsPerFrame;
            int count = snapshot.Count;
            carveCursor = count > 0 ? (carveCursor + 1) % count : 0;

            for (int n = 0; n < count && budget > 0; ++n)
            {
                VoxelBody body = snapshot[(carveCursor + n) % count];
                if (body == null || body.PendingCarves.Count == 0)
                    continue;

                if (body.ReleaseRequested || !body.IsAllocated)
                {
                    body.PendingCarves.Clear();
                    continue;
                }

                // A body being meshed, searched, emptied of pieces or cut on a worker is used by a job; its cuts wait.
                if (body.IsGenerating || body.IsBusy || body.InMeshBatch || islandInFlight && body == islandBody)
                    continue;

                int take = Mathf.Min(body.PendingCarves.Count, budget);
                int heavy = FirstHeavyCut(body.PendingCarves, take);
                if (heavy == 0)
                {
                    // One heavy cut at a time; the next waits for the worker.
                    if (heavyCarves.InFlight)
                        continue;

                    heavyCarves.Take(body, take);
                    budget -= take;
                    StartHeavyCarve(body);
                    continue;
                }

                // Cuts before a heavy one go now, in order; the heavy one starts next frame.
                if (heavy > 0)
                    take = heavy;

                inlineCarves.Take(body, take);
                budget -= take;
                inlineCarves.CreateJob(body).Run();
                ApplyCarveResults(body, inlineCarves);
            }
        }

        private static int FirstHeavyCut(List<CarveCommand> pending, int count)
        {
            for (int i = 0; i < count; ++i)
            {
                CarveRequest request = pending[i].Request;
                if (request.Shape != CarveShape.Sphere || request.Radius > HeavyCraterVoxels)
                    return i;
            }

            return -1;
        }

        private void StartHeavyCarve(VoxelBody body)
        {
            heavyCarves.Handle = heavyCarves.CreateJob(body).Schedule();
            heavyCarves.Body = body;
            heavyCarves.Version = body.Version;
            heavyCarves.InFlight = true;
            body.IsBusy = true;
            body.GridJob = heavyCarves.Handle;
            HeavyCuts++;
        }

        // A heavy cut still running is picked up next frame: the main thread never waits for it.
        private void FinishHeavyCarve()
        {
            if (!heavyCarves.InFlight || !heavyCarves.Handle.IsCompleted)
                return;

            heavyCarves.Handle.Complete();
            heavyCarves.InFlight = false;
            VoxelBody body = heavyCarves.Body;
            heavyCarves.Body = null;
            if (body == null)
                return;

            body.IsBusy = false;
            if (body.IsAllocated && !body.ReleaseRequested && body.Version == heavyCarves.Version)
                ApplyCarveResults(body, heavyCarves);
        }

        // Books what a finished batch of cuts did: mass, yields, chips, chunks to remesh and where to look for pieces.
        private void ApplyCarveResults(VoxelBody body, CarveBatch carves)
        {
            int count = carves.Commands.Count;
            float voxelVolume = Cube(body.VoxelSize);
            bool massChanged = false;
            for (int r = 0; r < count; ++r)
            {
                CarveCommand command = carves.Commands[r];
                int3 min = carves.Changed[r * 2];
                int3 max = carves.Changed[r * 2 + 1];
                if (!math.all(min <= max))
                    continue;

                body.Moments -= carves.Moments[r];
                massChanged = true;

                // The job placed the crack; chips and the split effect go where it landed.
                if (command.Request.Shape == CarveShape.Fracture)
                {
                    command.Point = body.GridToWorld((float3)(min + max) * 0.5f);
                    command.Radius = math.cmax(max - min) * body.VoxelSize * 0.25f;
                }

                int o = r * VoxelEncoding.MaterialSlots;
                NativeArray<float> removed = carves.Removed;
                var yield = new VoxelYield(body, command.Kind, command.Point,
                    removed[o] * voxelVolume, removed[o + 1] * voxelVolume, removed[o + 2] * voxelVolume,
                    removed[o + 3] * voxelVolume, removed[o + 4] * voxelVolume, removed[o + 5] * voxelVolume,
                    removed[o + 6] * voxelVolume, removed[o + 7] * voxelVolume);

                MarkDirty(body, min, max);
                body.LastCutPoint = command.Point;
                if (body.HasSplitRegion)
                {
                    body.SplitRegionMin = math.min(body.SplitRegionMin, min - 1);
                    body.SplitRegionMax = math.max(body.SplitRegionMax, max + 1);
                }
                else
                {
                    body.SplitRegionMin = min - 1;
                    body.SplitRegionMax = max + 1;
                    body.HasSplitRegion = true;
                }

                float total = yield.Total;
                if (total <= 0f)
                    continue;

                body.Volume -= total;
                system.Debris.Burst(command.Point, command.Normal, ChipColor(body.Definition, yield.DominantSlot), total, command.Radius);
                body.RaiseMaterialRemoved(yield);
                if (!body.IsWarmUp)
                    system.RecordYield(yield);
            }

            if (massChanged)
                body.UpdateMassProperties();

            if (body.HasSplitRegion && !body.IslandCheckQueued)
            {
                body.IslandCheckQueued = true;
                islandQueue.Add(body);
            }
        }

        // Twice a second: pieces far from the camera are removed, and rock switches mesh detail by distance.
        private void ReviewDistances()
        {
            if (Time.time < nextReview)
                return;

            nextReview = Time.time + ReviewInterval;
            if (!TryGetView(out Vector3 view))
                return;

            float rangeSq = system.PieceRange * system.PieceRange;
            foreach (VoxelBody body in bodies)
            {
                if (body == null || body.ReleaseRequested)
                    continue;

                if (body.IsPiece && (body.transform.position - view).sqrMagnitude > rangeSq)
                {
                    QueueRelease(body);
                    continue;
                }

                UpdateDetail(body, view);
            }
        }

        // Switches meshed rock to the detail its distance now calls for; its chunks remesh and cross-fade.
        private void UpdateDetail(VoxelBody body, Vector3 view)
        {
            if (!body.HasBeenMeshed || body.IsGenerating || !body.IsAllocated)
                return;

            int current = body.DetailLevel;
            int wanted = WantedDetail(body, view);
            if (wanted == current)
                return;

            // Coming closer must look right at once; moving away can take its time in the background.
            body.SetDetailLevel(wanted);
            bool isUrgent = wanted < current;
            for (int i = 0; i < body.Chunks.Length; ++i)
                Enqueue(body, i, isUrgent);
        }

        // Distant rock meshes at reduced detail, with colliders to match: half detail has a quarter of the triangles and
        // quarter detail a sixteenth. The distances scale with voxel size, so a cell covers about the same share of the
        // screen whenever rock switches.
        private int WantedDetail(VoxelBody body, Vector3 view)
        {
            float scale = body.VoxelSize / ReferenceVoxelSize;
            float voxelsAcross = body.EquivalentDiameter / body.VoxelSize;
            float distance = Vector3.Distance(view, body.CentreOfMass) - body.EquivalentDiameter * 0.5f;
            int current = body.DetailLevel;
            if (voxelsAcross < MinHalfDetailVoxels || !IsBeyond(distance, system.HalfDetailStart * scale, current >= 1))
                return 0;

            bool hasQuarter = voxelsAcross >= MinQuarterDetailVoxels && system.QuarterDetailStart > system.HalfDetailStart;
            return hasQuarter && IsBeyond(distance, system.QuarterDetailStart * scale, current >= 2) ? 2 : 1;
        }

        private static bool TryGetView(out Vector3 view)
        {
            Camera camera = Camera.main;
            view = camera != null ? camera.transform.position : Vector3.zero;
            return camera != null;
        }

        // Whether rock this far away is past a switch distance. Rock already past it must come a margin closer to switch
        // back, so it never flickers between details at the line.
        private static bool IsBeyond(float distance, float start, bool isPast)
        {
            return start > 0f && distance > start * (isPast ? 1f - DetailHysteresis : 1f + DetailHysteresis);
        }

        private void ScheduleIslands()
        {
            // One search at a time, and none while pieces are moving out: both share the search buffers.
            if (islandInFlight || extractionInFlight)
                return;

            VoxelBody body = null;
            for (int i = 0; i < islandQueue.Count && body == null; ++i)
            {
                VoxelBody next = islandQueue[i];
                if (next != null && next.IsBusy && !next.ReleaseRequested)
                    continue; // A heavy cut is still writing its grid; it stays in line.

                islandQueue.RemoveAt(i--);
                if (next == null)
                    continue;

                next.IslandCheckQueued = false;
                if (next.IsAllocated && !next.ReleaseRequested && !next.IsGenerating && next.HasSplitRegion)
                    body = next;
            }

            if (body == null)
                return;

            EnsureIslandCapacity(body.Density.Length);
            var job = new VoxelIslandJob
            {
                Density = body.Density,
                Material = body.Material,
                Size = body.GridSize,
                RegionMin = body.SplitRegionMin,
                RegionMax = body.SplitRegionMax,
                Labels = labels,
                Queue = queue,
                Parent = parent,
                UnionSize = unionSize,
                Frontier = frontier,
                Voxels = islandVoxels,
                Islands = islands,
                Summary = islandSummary
            };

            body.HasSplitRegion = false;
            islandBody = body;
            islandVersion = body.Version;
            islandHandle = job.Schedule();
            islandInFlight = true;
        }

        private void ScheduleMeshes()
        {
            // One batch at a time: it owns the shared scratch buffers until it is applied.
            if (meshInFlight)
                return;

            batch.Clear();
            Take(urgent, QueuedUrgent, system.UrgentChunksPerFrame);
            if (batch.Count < system.ChunksPerFrame)
                Take(background, QueuedBackground, system.ChunksPerFrame - batch.Count);

            if (batch.Count == 0)
                return;

            EnsureMeshSlots(batch.Count);
            meshData = Mesh.AllocateWritableMeshData(batch.Count);
            for (int i = 0; i < batch.Count; ++i)
            {
                VoxelBody body = batch[i].Body;
                requests[i] = MeshRequest.Create(body.Density, body.Material, body.GridSize, body.ChunkMin(batch[i].Index),
                    body.VoxelSize, body.GridOrigin, body.Palette, 1 << body.DetailLevel);
                body.InMeshBatch = true;
                batchMeshes.Add(AcquireMesh());
            }

            var job = new VoxelMeshJob
            {
                Requests = requests,
                Layout = layout,
                Blocks = blocks,
                BlockMaterials = blockMaterials,
                CellVertices = cellVertices,
                Vertices = vertices,
                Indices = indices,
                VertexCapacity = ChunkVertexCapacity,
                IndexCapacity = ChunkIndexCapacity,
                Results = results,
                Output = meshData
            };

            meshHandle = job.Schedule(batch.Count, 1);
            meshInFlight = true;
        }

        // Moves up to max current entries in the given state from the queue into the batch. Stale entries are dropped;
        // entries of a body whose pieces are moving out stay queued for a later frame.
        private void Take(List<ChunkRef> source, byte state, int max)
        {
            int taken = 0;
            int i = 0;
            for (; i < source.Count && taken < max; ++i)
            {
                ChunkRef entry = source[i];
                if (!entry.IsCurrent || entry.Body.IsGenerating || entry.Body.ChunkQueue[entry.Index] != state)
                    continue;

                if (entry.Body.IsBusy)
                {
                    deferred.Add(entry);
                    continue;
                }

                entry.Body.ChunkQueue[entry.Index] = NotQueued;
                batch.Add(entry);
                taken++;
            }

            source.RemoveRange(0, i);
            if (deferred.Count > 0)
            {
                source.InsertRange(0, deferred);
                deferred.Clear();
            }
        }

        private void ScheduleBakes()
        {
            // One cooking batch at a time; new shapes wait for the next.
            if (bakeInFlight)
                return;

            for (int i = 0; i < pendingBakeChunks.Count; ++i)
            {
                VoxelChunk chunk = pendingBakeChunks[i];
                if (chunk.IsAlive && chunk.RenderMesh == pendingBakeMeshes[i])
                {
                    bakingChunks.Add(chunk);
                    bakingMeshes.Add(pendingBakeMeshes[i]);
                }
            }

            pendingBakeChunks.Clear();
            pendingBakeMeshes.Clear();
            if (bakingChunks.Count == 0)
                return;

            EnsureCapacity(ref bakeIds, bakingChunks.Count, false);
            for (int i = 0; i < bakingMeshes.Count; ++i)
            {
                bakeIds[i] = bakingMeshes[i].GetEntityId();
                meshesInBake.Add(bakingMeshes[i]);
            }

            bakeHandle = new ColliderBakeJob { MeshIds = bakeIds }.Schedule(bakingMeshes.Count, 1);
            bakeInFlight = true;
        }

        /// <summary>Queues every chunk whose mesh reads any of the changed samples.</summary>
        private void MarkDirty(VoxelBody body, int3 changedMin, int3 changedMax)
        {
            // A chunk reads two samples beyond its own on every side.
            int3 lo = math.max(FloorDiv(changedMin - 2, SurfaceNets.ChunkSize), 0);
            int3 hi = math.min(FloorDiv(changedMax + 2, SurfaceNets.ChunkSize), body.ChunkCount - 1);
            for (int z = lo.z; z <= hi.z; ++z)
            {
                for (int y = lo.y; y <= hi.y; ++y)
                {
                    for (int x = lo.x; x <= hi.x; ++x)
                        Enqueue(body, body.ChunkIndex(new int3(x, y, z)), true);
                }
            }
        }

        private void Enqueue(VoxelBody body, int index, bool isUrgent)
        {
            byte state = body.ChunkQueue[index];
            if (state == QueuedUrgent || state == QueuedBackground && !isUrgent)
                return;

            if (state == NotQueued)
                body.PendingMeshes++;

            // A background entry promoted to urgent leaves a stale copy behind, which Take skips.
            body.ChunkQueue[index] = isUrgent ? QueuedUrgent : QueuedBackground;
            (isUrgent ? urgent : background).Add(new ChunkRef(body, index));
        }

        private Mesh AcquireMesh()
        {
            return meshPool.Count > 0 ? meshPool.Pop() : CreateMesh();
        }

        private Mesh CreateMesh()
        {
            var mesh = new Mesh { name = "Voxel Chunk" };
            mesh.MarkDynamic();
            ownedMeshes.Add(mesh);
            return mesh;
        }

        // A mesh still being cooked is returned to the pool only once its cooking job is done, and one a ghost still shows
        // only once its fade is over.
        private void ReleaseMesh(Mesh mesh)
        {
            if (mesh == null)
                return;

            if (bakeInFlight && meshesInBake.Contains(mesh))
            {
                releaseAfterBake.Add(mesh);
                return;
            }

            if (meshesInFade.Contains(mesh))
            {
                releaseAfterFade.Add(mesh);
                return;
            }

            mesh.Clear();
            meshPool.Push(mesh);
        }

        private void EnsureMeshSlots(int count)
        {
            if (count <= meshSlots)
                return;

            meshSlots = Mathf.Max(count, system.ChunksPerFrame);
            Reallocate(ref blocks, meshSlots * SurfaceNets.BlockLength);
            Reallocate(ref blockMaterials, meshSlots * SurfaceNets.BlockLength);
            Reallocate(ref cellVertices, meshSlots * SurfaceNets.CellMapLength);
            Reallocate(ref vertices, meshSlots * ChunkVertexCapacity);
            Reallocate(ref indices, meshSlots * ChunkIndexCapacity);
            Reallocate(ref requests, meshSlots);
            Reallocate(ref results, meshSlots);
        }

        private void EnsureIslandCapacity(int voxelCount)
        {
            if (labels.IsCreated && labels.Length >= voxelCount)
                return;

            // Labels must start at zero; every search leaves them zero again.
            DisposeArray(ref labels);
            labels = new NativeArray<ushort>(voxelCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            Reallocate(ref queue, voxelCount);
            Reallocate(ref islandVoxels, voxelCount);
        }

        private static Color ChipColor(AsteroidDefinition definition, int slot)
        {
            return definition != null ? definition.ChipColor(slot) : new Color(0.55f, 0.46f, 0.38f);
        }

        private static float Cube(float value)
        {
            return value * value * value;
        }

        private static int3 FloorDiv(int3 value, int divisor)
        {
            return (int3)math.floor((float3)value / divisor);
        }

        private static void EnsureCapacity<T>(ref NativeArray<T> array, int count, bool clear) where T : struct
        {
            if (array.IsCreated && array.Length >= count)
                return;

            DisposeArray(ref array);
            array = new NativeArray<T>(Mathf.Max(count, 16), Allocator.Persistent,
                clear ? NativeArrayOptions.ClearMemory : NativeArrayOptions.UninitializedMemory);
        }

        private static void Reallocate<T>(ref NativeArray<T> array, int count) where T : struct
        {
            DisposeArray(ref array);
            array = new NativeArray<T>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        }

        private static void DisposeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
                array.Dispose();
            array = default;
        }
    }
}
