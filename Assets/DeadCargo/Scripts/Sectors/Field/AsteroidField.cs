using System;
using System.Collections.Generic;
using System.Diagnostics;
using DeadCargo.Destruction;
using DeadCargo.Flow;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// The sector's asteroid fields, generated around the camera and drawn GPU-instanced in the comic rock look.
    /// <para>
    /// <b>Streaming.</b> Cells within draw distance that a field reaches are generated in a Burst job from the sector
    /// seed, a batch at a time, into fixed slots; cells left behind free theirs. Nothing generated is saved: a cell
    /// comes back the same every time. A batch still running is picked up a frame later; streaming waits for it.
    /// </para>
    /// <para>
    /// <b>Drawing.</b> Each frame, jobs started early in the frame decide which rocks are seen (in view, within draw
    /// distance, at least a pixel or two across), give each a level of detail from its size on screen, and write
    /// their matrices batch by batch; late in the frame the batches are drawn. Nothing pops: the smallest rocks grow in
    /// from nothing, each level of detail morphs from the one before across its whole span (the comic surface's
    /// detail morph), and the distance fade dissolves rocks at the draw distance. Detail is judged from the camera's
    /// path over the next moments, so at speed the rocks ahead are detailed before the camera reaches them.
    /// </para>
    /// <para>
    /// <b>Solid rocks.</b> Ten times a second a job picks the rocks near the player, nearest first, and each borrows a
    /// pooled collider in its own shape (<see cref="FieldRockColliders"/>). Hits on them are raised as
    /// <see cref="RockDamaged"/>.
    /// </para>
    /// Lives on the Sector Space object.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SectorSpace))]
    public sealed class AsteroidField : MonoBehaviour
    {
        // Instanced drawing takes at most this many matrices a call.
        private const int DrawBatch = 1023;
        private const int ScheduleBatch = 4;
        private const int MaxBatchCells = 128;
        private const float FallbackDrawDistance = 3000f;

        // The loading screen waits under this label until the rock shapes are cooked and the first cells are in.
        private const string BuildTaskLabel = "Building field";

        // Slack on the view planes, per metre of distance: covers the camera turning about 3 degrees in the frame
        // between culling and drawing.
        private const float ViewSlack = 0.05f;

        // Rocks grow from nothing at the smallest size drawn to full size at this many times it.
        private const float GrowPixels = 3f;

        // The comic surface's detail morph: its keyword, the per-draw size where a level of detail starts, and the
        // camera's path that detail is judged from.
        private const string MorphKeyword = "_GEOMORPH";
        private static readonly int FieldMorphId = Shader.PropertyToID("_FieldMorph");
        private static readonly int DetailAheadId = Shader.PropertyToID("_FieldDetailAhead");

        // A level morphs from the one before over this share of its span on screen. 1 is the whole span: a rock's
        // outline is always moving a little as it grows, and never jumps.
        private const float MorphSpan = 1f;

        // The camera's velocity is eased over this long, in seconds, so a shake or a stepped follow does not jitter
        // the detail. Faster than this, in metres a second, is a cut to a new place, not motion.
        private const float EyeSmoothing = 0.25f;
        private const float MaxEyeSpeed = 5000f;

        // Solid rocks are picked this often, in seconds, and stay solid this far past their range, in metres.
        private const float SolidInterval = 0.1f;
        private const float SolidKeepMargin = 40f;
        private const int MinSolidCandidates = 512;

        private static readonly ProfilerMarker StreamMarker = new ProfilerMarker("DeadCargo.Field.Stream");
        private static readonly ProfilerMarker DrawMarker = new ProfilerMarker("DeadCargo.Field.Draw");

        [Tooltip("What the fields are made of: rock density, sizes, shapes and tumble.")]
        [SerializeField] private AsteroidFieldDefinition definition;

        [Tooltip("The rock material. It must allow GPU instancing and use the comic surface's Triplanar mode, as the " +
                 "rocks have no texture coordinates.")]
        [SerializeField] private Material material;

        [Tooltip("How far rocks are drawn, in metres. 0 uses the camera's far clip, where the distance fade also ends.")]
        [SerializeField, Min(0f)] private float drawDistance;

        [Tooltip("Level of detail. Above 1 switches to finer meshes sooner; below 1 later.")]
        [SerializeField, Range(0.25f, 4f)] private float detailBias = 1f;

        [Tooltip("Rocks smaller than this on screen, in pixels across, are not drawn.")]
        [SerializeField, Range(0.5f, 8f)] private float minPixels = 1.5f;

        [Tooltip("Most rocks drawn in one frame. Past this, the rest wait for a frame with fewer.")]
        [SerializeField, Range(1000, 200000)] private int maxDrawn = 100000;

        [Tooltip("Rocks within the shadow distance cast shadows. They always receive them.")]
        [SerializeField] private bool castShadows = true;

        [Tooltip("How far ahead of a moving camera detail is made ready, in seconds of travel. Higher shows rocks ahead " +
                 "in full detail sooner at speed, for more triangles; 0 judges detail from where the camera is.")]
        [SerializeField, Range(0f, 4f)] private float detailLookAhead = 1.5f;

        [Tooltip("Rocks near the player are solid: ships hit them and weapons stop on them.")]
        [SerializeField] private bool solidRocks = true;

        [Tooltip("A rock is solid while its surface is within this distance of the player, in metres.")]
        [SerializeField, Range(50f, 1000f)] private float solidRange = 200f;

        [Tooltip("Larger rocks are solid from further off: this many metres more for every metre of radius.")]
        [SerializeField, Range(0f, 60f)] private float solidRangePerRadius = 20f;

        [Tooltip("Most rocks solid at once. Past this, the nearest are kept.")]
        [SerializeField, Range(16, 1024)] private int maxSolidRocks = 256;

        private readonly Dictionary<CellCoord, int> slotOfCell = new Dictionary<CellCoord, int>();
        private readonly HashSet<CellCoord> wanted = new HashSet<CellCoord>();
        private readonly List<CellCoord> nearestFirst = new List<CellCoord>();
        private readonly CellDistance cellDistance = new CellDistance();
        private readonly List<CellCoord> leaving = new List<CellCoord>();
        private readonly Stack<int> freeSlots = new Stack<int>();
        private readonly Plane[] planes = new Plane[6];
        private readonly Stopwatch timer = new Stopwatch();
        private readonly FieldRockMeshes meshes = new FieldRockMeshes();
        private readonly MaterialPropertyBlock[] detailMorphs = new MaterialPropertyBlock[FieldRockShape.Levels];

        private SectorSpace space;
        private FieldSettings settings;
        private SectorGrid grid;
        private uint seed;
        private int capacity;
        private int slots;
        private int batches;
        private float loadRadius;
        private bool isReady;
        private bool needsRebuild;
        private bool needsStream;
        private CellCoord streamedCell;
        private bool slotsReported;
        private LoadTask buildTask;

        private NativeArray<RegionVolume> regions;
        private NativeArray<FieldRock> rocks;
        private NativeArray<int> counts;

        // A generation batch, nearest cells first: its cells and slots, and the rocks it writes before they are moved
        // into the slots.
        private NativeArray<CellCoord> batchCells;
        private NativeArray<int> batchSlots;
        private NativeArray<FieldRock> batchRocks;
        private NativeArray<int> batchCounts;
        private int batchSize;
        private JobHandle generation;
        private bool isGenerating;

        // Per slot: its cell's lowest corner, and how far culling got through its rocks this frame.
        private NativeArray<float3> slotMins;
        private NativeArray<int> examined;

        private NativeArray<float4> viewPlanes;
        private NativeArray<byte> rockBatches;
        private NativeArray<int> batchCountsBySlot;
        private NativeArray<int> batchOffsets;
        private NativeArray<int2> batchRanges;
        private NativeArray<Matrix4x4> matrices;
        private JobHandle drawing;
        private bool isDrawScheduled;
        private float frameDrawDistance;
        private float streamMilliseconds;

        // The camera's eased velocity in sector space, and this frame's path ahead of it.
        private float3 lastEye;
        private float3 eyeVelocity;
        private float3 frameAhead;
        private bool hasLastEye;

        private readonly FieldDensityMap densityMap = new FieldDensityMap();
        private float3 clumpOffset;

        private FieldRockColliders colliders;
        private NativeArray<SolidCandidate> solidCandidates;
        private NativeArray<int> solidFound;
        private JobHandle solidSelection;
        private bool isSolidScheduled;
        private float nextSolidTime;

        /// <summary>The field for the loaded scene, or null.</summary>
        public static AsteroidField Active { get; private set; }

        /// <summary>
        /// Raised when a weapon hits a solid rock, with its stand-in, which names the rock. Where carving a field rock
        /// into a voxel asteroid plugs in.
        /// </summary>
        public event Action<FieldRockProxy, DamageEvent> RockDamaged;

        /// <summary>Whether rocks near the player are solid. For tools.</summary>
        public bool SolidRocks
        {
            get => solidRocks;
            set => solidRocks = value;
        }

        /// <summary>Rocks solid now.</summary>
        public int SolidRockCount => colliders != null ? colliders.Count : 0;

        /// <summary>How thick the field is at the camera, 0 to 1, clumps and gaps included.</summary>
        public float DensityHere { get; private set; }

        public AsteroidFieldDefinition Definition => definition;

        /// <summary>The rock material the field draws with.</summary>
        public Material Material => material;

        /// <summary>Cells generated and held.</summary>
        public int LoadedCells => slotOfCell.Count;

        /// <summary>Slots for cells; past this, further cells wait.</summary>
        public int SlotCount => slots;

        /// <summary>Rocks generated and held, across every loaded cell.</summary>
        public int LoadedRocks { get; private set; }

        /// <summary>Rocks drawn last frame.</summary>
        public int DrawnRocks { get; private set; }

        /// <summary>Instanced draw calls last frame, each drawing up to 1,023 rocks.</summary>
        public int DrawCalls { get; private set; }

        /// <summary>True while a batch of cells is being generated.</summary>
        public bool IsGenerating => isGenerating;

        /// <summary>Main-thread time of the last frame's streaming and drawing, in milliseconds.</summary>
        public float MainThreadMilliseconds { get; private set; }

        /// <summary>Rocks per cell at full density, every candidate kept.</summary>
        public int CellCapacity => capacity;

        /// <summary>Generates every cell again, with the current definition and seed. For tools after a change.</summary>
        public void Rebuild()
        {
            needsRebuild = true;
        }

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
        }

        private void Awake()
        {
            space = GetComponent<SectorSpace>();
        }

        private void OnEnable()
        {
            Active = this;
            if (space != null)
                space.SeedChanged += Rebuild;

            needsRebuild = true;
        }

        private void OnDisable()
        {
            if (space != null)
                space.SeedChanged -= Rebuild;
            if (Active == this)
                Active = null;

            Release();
        }

        private void OnDestroy()
        {
            Release();
            colliders?.Dispose();
            colliders = null;
            meshes.Dispose();
        }

        private void FixedUpdate()
        {
            if (isReady && colliders != null)
                colliders.Step(Time.fixedTime);
        }

        private void Update()
        {
            timer.Restart();
            using (StreamMarker.Auto())
            {
                // Normally finished in late update; a frame that skipped it must not leave the jobs holding the rocks.
                if (isDrawScheduled)
                {
                    drawing.Complete();
                    isDrawScheduled = false;
                }

                if (isSolidScheduled)
                {
                    solidSelection.Complete();
                    isSolidScheduled = false;
                }

                if (needsRebuild)
                    Initialise();

                if (isReady)
                {
                    using LoadScope streaming = LoadReport.Measure("Field streaming");
                    FinishGeneration();
                    Camera view = Camera.main;
                    if (view != null)
                    {
                        Stream(view);
                        ScheduleDrawing(view);
                    }

                    ScheduleSolid();
                    ReportBuild();
                }
            }

            timer.Stop();
            streamMilliseconds = (float)timer.Elapsed.TotalMilliseconds;
        }

        // Half for the rock shapes, half for the first cells round the camera: streamed once, with no batch left to run.
        private void ReportBuild()
        {
            if (buildTask == null)
                return;

            bool shapesReady = colliders.IsReady;
            bool cellsIn = !needsStream && !isGenerating;
            buildTask.Report((shapesReady ? 0.5f : 0f) + (cellsIn ? 0.5f : 0f));
            if (shapesReady && cellsIn)
            {
                buildTask.Complete();
                buildTask = null;
            }
        }

        private void LateUpdate()
        {
            timer.Restart();
            using (DrawMarker.Auto())
            {
                Draw();
                ApplySolid();
            }

            timer.Stop();
            MainThreadMilliseconds = streamMilliseconds + (float)timer.Elapsed.TotalMilliseconds;
        }

        // Sizes every buffer for the definition, grid and draw distance, builds the rock shapes, and starts streaming afresh.
        private void Initialise()
        {
            using LoadScope measure = LoadReport.Measure("Asteroid field");
            needsRebuild = false;
            Release();

            SectorDefinition sector = space != null ? space.Definition : null;
            if (sector == null || definition == null || material == null)
                return;

            if (!material.enableInstancing)
            {
                Debug.LogWarning($"The asteroid field's material '{material.name}' does not allow GPU instancing, so no rocks are drawn. " +
                                 "Run Set Up Sector again.", this);
                return;
            }

            grid = space.Grid;
            seed = space.Seed;
            settings = definition.ToSettings(grid.CellSize);
            capacity = settings.CellCapacity;
            meshes.Build(seed, settings.Shapes);
            PrepareDetailMorphs();
            batches = FieldRockShape.Levels * meshes.Shapes * 2;
            colliders ??= new FieldRockColliders(this);
            colliders.Prepare(meshes, maxSolidRocks);

            Camera view = Camera.main;
            loadRadius = DrawDistanceFor(view) + settings.MaxReach;
            int ring = (int)math.ceil(loadRadius / grid.CellSize);
            int across = ring * 2 + 1;
            slots = math.min(across, grid.CountX) * math.min(across, grid.CountY) * math.min(across, grid.CountZ);

            // The run's layout, so the field follows this run's regions and the clearings round placed content.
            SectorLayout layout = space.Layout;
            regions = new NativeArray<RegionVolume>(layout.Count, Allocator.Persistent);
            for (int i = 0; i < layout.Count; ++i)
                regions[i] = layout[i];

            clumpOffset = FieldGeneration.ClumpOffset(seed);
            densityMap.Build(regions, settings, grid, seed, space.SectorToWorld(grid.Min));

            int batchCapacity = math.min(slots, MaxBatchCells);
            rocks = new NativeArray<FieldRock>(slots * capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            counts = new NativeArray<int>(slots, Allocator.Persistent);
            batchCells = new NativeArray<CellCoord>(batchCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            batchSlots = new NativeArray<int>(batchCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            batchRocks = new NativeArray<FieldRock>(batchCapacity * capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            batchCounts = new NativeArray<int>(batchCapacity, Allocator.Persistent);

            slotMins = new NativeArray<float3>(slots, Allocator.Persistent);
            examined = new NativeArray<int>(slots, Allocator.Persistent);
            solidCandidates = new NativeArray<SolidCandidate>(math.max(MinSolidCandidates, maxSolidRocks * 2), Allocator.Persistent);
            solidFound = new NativeArray<int>(1, Allocator.Persistent);
            nextSolidTime = 0f;
            hasLastEye = false;
            eyeVelocity = float3.zero;

            viewPlanes = new NativeArray<float4>(4, Allocator.Persistent);
            rockBatches = new NativeArray<byte>(slots * capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            batchCountsBySlot = new NativeArray<int>(slots * batches, Allocator.Persistent);
            batchOffsets = new NativeArray<int>(slots * batches, Allocator.Persistent);
            batchRanges = new NativeArray<int2>(batches, Allocator.Persistent);
            matrices = new NativeArray<Matrix4x4>(maxDrawn, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            freeSlots.Clear();
            for (int slot = slots - 1; slot >= 0; --slot)
                freeSlots.Push(slot);

            slotOfCell.Clear();
            LoadedRocks = 0;
            slotsReported = false;
            needsStream = true;
            isReady = true;
            buildTask = LoadGate.Begin(BuildTaskLabel);
        }

        // Frees every buffer once no job uses them. A rebuild or teardown is the only place this waits for jobs.
        private void Release()
        {
            generation.Complete();
            drawing.Complete();
            solidSelection.Complete();
            isGenerating = false;
            isDrawScheduled = false;
            isSolidScheduled = false;
            isReady = false;
            DrawnRocks = 0;

            // A field torn down mid-load holds the loading screen no longer.
            buildTask?.Complete();
            buildTask = null;
            DrawCalls = 0;

            // The rock shapes are about to be rebuilt or destroyed, so nothing may stay solid on them.
            colliders?.Reset();
            Shader.SetGlobalVector(DetailAheadId, Vector4.zero);
            densityMap.Clear();
            DensityHere = 0f;

            DisposeArray(ref slotMins);
            DisposeArray(ref examined);
            DisposeArray(ref solidCandidates);
            DisposeArray(ref solidFound);
            DisposeArray(ref regions);
            DisposeArray(ref rocks);
            DisposeArray(ref counts);
            DisposeArray(ref batchCells);
            DisposeArray(ref batchSlots);
            DisposeArray(ref batchRocks);
            DisposeArray(ref batchCounts);
            DisposeArray(ref viewPlanes);
            DisposeArray(ref rockBatches);
            DisposeArray(ref batchCountsBySlot);
            DisposeArray(ref batchOffsets);
            DisposeArray(ref batchRanges);
            DisposeArray(ref matrices);
            slotOfCell.Clear();
            freeSlots.Clear();
            LoadedRocks = 0;
        }

        // A finished batch moves into its slots; one still running is picked up next frame.
        private void FinishGeneration()
        {
            if (!isGenerating || !generation.IsCompleted)
                return;

            generation.Complete();
            isGenerating = false;
            for (int i = 0; i < batchSize; ++i)
            {
                int slot = batchSlots[i];
                if (!slotOfCell.TryGetValue(batchCells[i], out int held) || held != slot)
                    continue;

                int made = batchCounts[i];
                NativeArray<FieldRock>.Copy(batchRocks, i * capacity, rocks, slot * capacity, made);
                counts[slot] = made;
                LoadedRocks += made;
            }
        }

        // When the camera enters a new cell: frees the cells now out of range and generates the ones now in it.
        private void Stream(Camera view)
        {
            if (isGenerating)
                return;

            float3 eye = space.WorldToSector(view.transform.position);
            CellCoord centre = grid.CellAt(eye);
            if (!needsStream && centre == streamedCell)
                return;

            needsStream = false;
            streamedCell = centre;
            FindWantedCells(eye, centre);

            leaving.Clear();
            foreach (KeyValuePair<CellCoord, int> loaded in slotOfCell)
            {
                if (!wanted.Contains(loaded.Key))
                    leaving.Add(loaded.Key);
            }

            foreach (CellCoord cell in leaving)
            {
                int slot = slotOfCell[cell];
                slotOfCell.Remove(cell);
                LoadedRocks -= counts[slot];
                counts[slot] = 0;
                colliders.ReleaseSlot(slot, capacity);
                freeSlots.Push(slot);
            }

            batchSize = 0;
            foreach (CellCoord cell in nearestFirst)
            {
                if (slotOfCell.ContainsKey(cell))
                    continue;

                // A full batch: the rest follow in the next, once this one is in.
                if (batchSize == batchCells.Length)
                {
                    needsStream = true;
                    break;
                }

                if (freeSlots.Count == 0)
                {
                    if (!slotsReported)
                    {
                        slotsReported = true;
                        Debug.LogWarning($"The asteroid field ran out of its {slots} cell slots; some cells are not drawn. " +
                                         "The camera's far clip may have grown since the field started.", this);
                    }

                    break;
                }

                int slot = freeSlots.Pop();
                slotOfCell.Add(cell, slot);
                slotMins[slot] = grid.CellMin(cell);
                batchCells[batchSize] = cell;
                batchSlots[batchSize] = slot;
                ++batchSize;
            }

            if (batchSize == 0)
                return;

            generation = new FieldCellJob
            {
                Cells = batchCells,
                Regions = regions,
                Settings = settings,
                Grid = grid,
                SectorSeed = seed,
                Rocks = batchRocks,
                Counts = batchCounts
            }.Schedule(batchSize, 1);

            isGenerating = true;
            JobHandle.ScheduleBatchedJobs();
        }

        // Cells in the grid, within reach of the camera, that a field reaches, nearest first.
        private void FindWantedCells(float3 eye, CellCoord centre)
        {
            wanted.Clear();
            nearestFirst.Clear();
            var regionSet = new NativeRegionSet(regions);
            int ring = (int)math.ceil(loadRadius / grid.CellSize);
            float reachSquared = loadRadius * loadRadius;
            for (int z = -ring; z <= ring; ++z)
            {
                for (int y = -ring; y <= ring; ++y)
                {
                    for (int x = -ring; x <= ring; ++x)
                    {
                        var cell = new CellCoord(centre.X + x, centre.Y + y, centre.Z + z);
                        if (!FieldGeneration.MayHoldRocks(regionSet, grid, cell))
                            continue;

                        float3 min = grid.CellMin(cell);
                        float3 nearest = math.clamp(eye, min, min + grid.CellSize);
                        if (math.distancesq(eye, nearest) <= reachSquared)
                        {
                            wanted.Add(cell);
                            nearestFirst.Add(cell);
                        }
                    }
                }
            }

            cellDistance.Centre = centre;
            nearestFirst.Sort(cellDistance);
        }

        // Starts this frame's culling and matrix jobs from the camera as it was last drawn, with slack for its turn.
        private void ScheduleDrawing(Camera view)
        {
            frameDrawDistance = DrawDistanceFor(view);
            GeometryUtility.CalculateFrustumPlanes(view, planes);
            float3 offset = space.SectorToWorld(Vector3.zero);
            for (int p = 0; p < 4; ++p)
            {
                // Planes come in world space; rocks are kept in sector space.
                float3 normal = planes[p].normal;
                viewPlanes[p] = new float4(normal, planes[p].distance + math.dot(normal, offset));
            }

            float3 eye = space.WorldToSector(view.transform.position);
            frameAhead = PathAhead(eye);

            // How deep in a field the camera is, for effects that thicken there, such as space dust.
            DensityHere = SectorDensity.DensityOf(new NativeRegionSet(regions), SectorRegionKind.AsteroidField, eye) *
                          FieldGeneration.Clump(settings, clumpOffset, eye);
            FieldDensityMap.SetDensityHere(DensityHere);
            float pixelsPerRadian = view.pixelHeight / (2f * math.tan(math.radians(view.fieldOfView) * 0.5f));
            JobHandle cull = new FieldCullJob
            {
                Rocks = rocks,
                Counts = counts,
                SlotMins = slotMins,
                Planes = viewPlanes,
                Capacity = capacity,
                Batches = batches,
                Shapes = meshes.Shapes,
                CellSize = grid.CellSize,
                Eye = eye,
                Ahead = frameAhead,
                DrawDistance = frameDrawDistance,
                ShadowDistance = castShadows ? QualitySettings.shadowDistance : -1f,
                PixelsPerRadian = pixelsPerRadian,
                MinPixels = minPixels,
                DetailBias = detailBias,
                Reach = (1f + settings.Stretch) * FieldRockShape.MaxRadius,
                Stretch = settings.Stretch,
                Slack = ViewSlack,
                RockBatches = rockBatches,
                BatchCounts = batchCountsBySlot,
                Examined = examined
            }.Schedule(slots, ScheduleBatch);

            JobHandle layout = new FieldOffsetJob
            {
                BatchCounts = batchCountsBySlot,
                BatchOffsets = batchOffsets,
                BatchRanges = batchRanges,
                Slots = slots,
                Batches = batches,
                MaxDrawn = matrices.Length
            }.Schedule(cull);

            drawing = new FieldScatterJob
            {
                Rocks = rocks,
                RockBatches = rockBatches,
                Examined = examined,
                BatchOffsets = batchOffsets,
                Settings = settings,
                Capacity = capacity,
                Batches = batches,
                MaxDrawn = matrices.Length,
                Time = Time.time,
                OriginOffset = offset,
                Eye = eye,
                Ahead = frameAhead,
                PixelsPerRadian = pixelsPerRadian,
                MinPixels = minPixels,
                GrowPixels = GrowPixels,
                Matrices = matrices
            }.Schedule(slots, ScheduleBatch, layout);

            isDrawScheduled = true;
            JobHandle.ScheduleBatchedJobs();
        }

        // From the camera to where it will be after the look-ahead, from its eased velocity.
        private float3 PathAhead(float3 eye)
        {
            float delta = Time.deltaTime;
            if (hasLastEye && delta > 1e-5f)
            {
                float3 velocity = (eye - lastEye) / delta;
                if (math.lengthsq(velocity) > MaxEyeSpeed * MaxEyeSpeed)
                    eyeVelocity = float3.zero;
                else
                    eyeVelocity = math.lerp(eyeVelocity, velocity, 1f - math.exp(-delta / EyeSmoothing));
            }

            lastEye = eye;
            hasLastEye = true;
            return eyeVelocity * detailLookAhead;
        }

        // Starts a pass picking the rocks near the player to be solid. It runs alongside the frame's drawing jobs and
        // is applied in late update.
        private void ScheduleSolid()
        {
            if (!solidRocks)
            {
                if (colliders.Count > 0)
                    colliders.ReleaseAll();
                return;
            }

            if (Time.time < nextSolidTime || !colliders.IsReady)
                return;

            nextSolidTime = Time.time + SolidInterval;
            solidSelection = new FieldSolidJob
            {
                Rocks = rocks,
                Counts = counts,
                SlotMins = slotMins,
                Slots = slots,
                Capacity = capacity,
                CellSize = grid.CellSize,
                Focus = space.FocusPosition,
                Reach = (1f + settings.Stretch) * FieldRockShape.MaxRadius,
                Range = solidRange,
                RangePerRadius = solidRangePerRadius,
                KeepMargin = SolidKeepMargin,
                Candidates = solidCandidates,
                Found = solidFound
            }.Schedule();

            isSolidScheduled = true;
            JobHandle.ScheduleBatchedJobs();
        }

        private void ApplySolid()
        {
            if (!isSolidScheduled)
                return;

            solidSelection.Complete();
            isSolidScheduled = false;
            colliders.Apply(solidCandidates, solidFound[0], rocks, settings, meshes, space, maxSolidRocks, Time.time);
        }

        internal void ReportRockDamaged(FieldRockProxy proxy, in DamageEvent damage)
        {
            RockDamaged?.Invoke(proxy, damage);
        }

        private void Draw()
        {
            if (!isDrawScheduled)
                return;

            drawing.Complete();
            isDrawScheduled = false;

            // The detail morph judges size on screen from the same path the jobs used.
            Shader.SetGlobalVector(DetailAheadId, (Vector3)frameAhead);

            Camera view = Camera.main;
            Vector3 centre = view != null ? view.transform.position : Vector3.zero;
            var bounds = new Bounds(centre, Vector3.one * (2f * (frameDrawDistance + settings.MaxReach)));
            var plain = new RenderParams(material)
            {
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
                layer = gameObject.layer
            };

            RenderParams shadowed = plain;
            shadowed.shadowCastingMode = ShadowCastingMode.On;
            UpdateDetailMorphs();

            int drawn = 0;
            int calls = 0;
            int shapes = meshes.Shapes;
            for (int batch = 0; batch < batches; ++batch)
            {
                int2 range = batchRanges[batch];
                if (range.y == 0)
                    continue;

                int kind = batch >> 1;
                int detail = kind / shapes;
                Mesh mesh = meshes.Get(kind % shapes, detail);
                RenderParams parameters = (batch & 1) != 0 ? shadowed : plain;
                parameters.matProps = detailMorphs[detail];
                int end = range.x + range.y;
                for (int start = range.x; start < end; start += DrawBatch)
                {
                    Graphics.RenderMeshInstanced(parameters, mesh, 0, matrices, math.min(DrawBatch, end - start), start);
                    ++calls;
                }

                drawn += range.y;
            }

            DrawnRocks = drawn;
            DrawCalls = calls;
        }

        private void PrepareDetailMorphs()
        {
            for (int level = 0; level < FieldRockShape.Levels; ++level)
                detailMorphs[level] ??= new MaterialPropertyBlock();

            UpdateDetailMorphs();
            if (!material.IsKeywordEnabled(MorphKeyword))
                Debug.Log($"The asteroid field's material '{material.name}' has no detail morph, so rocks change detail with a " +
                          "small pop. Run Set Up Sector again to turn it on.", this);
        }

        // Each level of detail tells the comic surface the size on screen where it starts and the share of its span it
        // morphs over, so its vertices slide from the level before as the rock grows: level k starts where the
        // culling job switches to it. Set every frame, as the detail bias may be changed in Play Mode.
        private void UpdateDetailMorphs()
        {
            for (int level = 0; level < FieldRockShape.Levels; ++level)
            {
                float start = level == 0 ? 0f : FieldCullJob.FirstDetailPixels * (1 << (level - 1)) / detailBias;
                detailMorphs[level].SetVector(FieldMorphId, new Vector4(start, MorphSpan, 0f, 0f));
            }
        }

        private float DrawDistanceFor(Camera view)
        {
            if (drawDistance > 0f)
                return drawDistance;

            return view != null ? view.farClipPlane : FallbackDrawDistance;
        }

        private static void DisposeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
                array.Dispose();
            array = default;
        }

        // Orders cells by how many cells away from the centre they are.
        private sealed class CellDistance : IComparer<CellCoord>
        {
            public CellCoord Centre;

            public int Compare(CellCoord a, CellCoord b)
            {
                return Steps(a).CompareTo(Steps(b));
            }

            private int Steps(CellCoord cell)
            {
                int x = cell.X - Centre.X;
                int y = cell.Y - Centre.Y;
                int z = cell.Z - Centre.Z;
                return x * x + y * y + z * z;
            }
        }
    }
}
