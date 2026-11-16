using System;
using System.Collections.Generic;
using System.Diagnostics;
using DeadCargo.Destruction.Voxels;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DeadCargo.Destruction
{
    /// <summary>
    /// The scene service behind everything destructible. Runs the voxel pipeline for all bodies once a frame, moves
    /// every rock along its drift and tumble each physics step, keeps the pools (pieces, meshes, effects), throws rock
    /// chips, and reports what was removed so mining and other systems can react. With the scene's first asteroid it
    /// warms its own code paths up out of sight (<see cref="DestructionWarmUp"/>). Lives in the Systems folder; one per
    /// scene.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class DestructionSystem : MonoBehaviour
    {
        private const string PoolName = "Piece Pool";

        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("DeadCargo.Destruction.Tick");
        private static readonly ProfilerMarker TrailsMarker = new ProfilerMarker("DeadCargo.Destruction.Trails");
        private static readonly ProfilerMarker ChipsMarker = new ProfilerMarker("DeadCargo.Destruction.Chips");

        [Header("Start-Up")]
        [Tooltip("When the scene builds its first asteroid, also build, cut and crack a small hidden rock, and fill the " +
                 "mesh and piece pools. Code running for the first time is slow, and this moves that cost from the " +
                 "player's first hit to the load.")]
        [SerializeField] private bool warmUpOnLoad = true;

        [Header("Meshing")]
        [Tooltip("Chunks rebuilt per frame for rock that is still appearing. Lower values spread new asteroids over more frames.")]
        [SerializeField, Range(1, 64)] private int chunksPerFrame = 8;

        [Tooltip("Chunks rebuilt per frame for cuts and breaks, which should show at once. Rarely reached.")]
        [SerializeField, Range(1, 128)] private int urgentChunksPerFrame = 48;

        [Tooltip("Cuts applied per frame across all rocks. The rest wait for the next frame, oldest first.")]
        [SerializeField, Range(1, 512)] private int cutsPerFrame = 32;

        [Tooltip("Cooked colliders switched to their new shape per frame. The rest follow next frame, colliding with their " +
                 "previous shape meanwhile.")]
        [SerializeField, Range(1, 128)] private int collidersPerFrame = 16;

        [Tooltip("Rock further than this from the camera, in metres, meshes at half detail: a quarter of the triangles. " +
                 "Set for 0.5 m voxels, where a cell then spans about 5 pixels at 1080p; rock with larger voxels switches " +
                 "proportionally further out. 0 keeps full detail everywhere.")]
        [SerializeField, Min(0f)] private float halfDetailStart = 200f;

        [Tooltip("Rock further than this from the camera, in metres, meshes at quarter detail: a sixteenth of the " +
                 "triangles. Set for 0.5 m voxels, like the half detail start. 0, or no further than the half detail " +
                 "start, stops at half detail.")]
        [SerializeField, Min(0f)] private float quarterDetailStart = 400f;

        [Tooltip("Seconds rock takes to cross-fade from one mesh detail to the other, through a fine dither, so a switch " +
                 "never pops. 0 switches at once.")]
        [SerializeField, Range(0f, 2f)] private float detailFadeTime = 0.5f;

        [Header("Pieces")]
        [Tooltip("Smallest piece, in voxels, that breaks away as a rock of its own. Smaller pieces crumble into chips.")]
        [SerializeField, Min(1)] private int minPieceVoxels = 60;

        [Tooltip("Most free pieces at once. Past this, the smallest crumble into chips.")]
        [SerializeField, Range(1, 128)] private int maxFreePieces = 24;

        [Tooltip("Free pieces further than this from the camera are removed, in metres.")]
        [SerializeField, Min(100f)] private float pieceRange = 3000f;

        [Header("Chips")]
        [Tooltip("Material for rock chips. Leave empty to use the built-in unlit particle shader.")]
        [SerializeField] private Material chipMaterial;

        [Tooltip("Most chips in the air at once.")]
        [SerializeField, Range(64, 8192)] private int maxChips = 2048;

        [Tooltip("Chips per cubic metre of rock removed.")]
        [SerializeField, Min(0f)] private float chipsPerCubicMetre = 6f;

        [Tooltip("Most chips from one cut.")]
        [SerializeField, Range(1, 64)] private int maxChipsPerCut = 20;

        [Tooltip("Most chips thrown per second, shared between the cuts that land. Held below what the chip pool can " +
                 "keep in the air, so the pool never fills and the newest cut always throws chips.")]
        [SerializeField, Range(30, 8192)] private int maxChipsPerSecond = 750;

        [Tooltip("Size of a chip from a one-metre cut, in metres.")]
        [SerializeField, Min(0.01f)] private float chipSize = 0.35f;

        [Header("Trail Rocks")]
        [Tooltip("Most trail rocks in the scene at once, across every asteroid. 0 turns trails off. Each asteroid type " +
                 "sets its own trail.")]
        [SerializeField, Range(0, 8192)] private int maxTrailRocks = 1536;

        [Tooltip("Asteroids further than this from the camera, in metres, grow no trail.")]
        [SerializeField, Min(0f)] private float trailRange = 2500f;

        [Tooltip("Trail rocks within this distance of the camera, in metres, are solid: ships hit them and weapons " +
                 "shatter them. Further out they only drift, at almost no cost.")]
        [SerializeField, Min(0f)] private float trailPhysicsRange = 150f;

        [Tooltip("Most trail rocks solid at once. The nearest win.")]
        [SerializeField, Range(0, 256)] private int maxSolidTrailRocks = 64;

        [Tooltip("Rigidbody mass per tonne of solid trail rock, against kit ships that weigh 1. At 0.005 a 1 m rock " +
                 "weighs about 0.006 and a 2 m rock 0.04: a ship knocks them flying and feels a tap, or a firm knock " +
                 "from a big one at speed.")]
        [SerializeField, Min(0f)] private float solidRockMassPerTonne = 0.005f;

        [Tooltip("How much a ship feels a trail rock it hits, 0 to 1. 1 is plain physics; lower values make the ship act " +
                 "that many times heavier in the hit, so the rock still flies off but the ship barely moves, however fast " +
                 "it was going. Turning is softened the same way.")]
        [SerializeField, Range(0f, 1f)] private float shipKick = 0.15f;

        [Tooltip("Material for trail rocks; it must allow GPU instancing. Empty uses the asteroid's own rock material.")]
        [SerializeField] private Material trailMaterial;

        private readonly Dictionary<VoxelMaterialDefinition, float> removedByMaterial = new Dictionary<VoxelMaterialDefinition, float>();
        private readonly List<VoxelBody> piecePool = new List<VoxelBody>();
        private readonly Stopwatch timer = new Stopwatch();
        private VoxelScheduler scheduler;
        private DestructionWarmUp warmUp;
        private Transform poolRoot;
        private int pieceCount;
        private float trailStartMilliseconds;

        /// <summary>The system for the loaded scene, or null.</summary>
        public static DestructionSystem Active { get; private set; }

        /// <summary>Raised for every cut on any voxel body, with what it removed.</summary>
        public event Action<VoxelYield> MaterialRemoved;

        /// <summary>Raised when a piece breaks away from any body, with the body and the new piece.</summary>
        public event Action<VoxelBody, VoxelBody> BodySplit;

        public IReadOnlyList<VoxelBody> Bodies => scheduler != null ? scheduler.Bodies : (IReadOnlyList<VoxelBody>)Array.Empty<VoxelBody>();

        /// <summary>Rock removed this session, per material, in cubic metres.</summary>
        public IReadOnlyDictionary<VoxelMaterialDefinition, float> RemovedByMaterial => removedByMaterial;

        public int QueuedChunks => scheduler != null ? scheduler.QueuedChunks : 0;
        public int MeshedLastFrame => scheduler != null ? scheduler.MeshedLastFrame : 0;
        public int PieceSearches => scheduler != null ? scheduler.PieceSearches : 0;
        public int LastSearchVisited => scheduler != null ? scheduler.LastSearchVisited : 0;

        /// <summary>Cuts across whole rocks made on a worker thread since the scene started.</summary>
        public int HeavyCuts => scheduler != null ? scheduler.HeavyCuts : 0;
        public long ScratchBytes => scheduler != null ? scheduler.ScratchBytes : 0;
        public int ChipCount => Debris != null ? Debris.Count : 0;

        /// <summary>Trail rocks in the scene.</summary>
        public int TrailRockCount => Trails != null ? Trails.Count : 0;

        /// <summary>Trail rocks solid right now: near the camera, where ships can hit them.</summary>
        public int SolidTrailRocks => Trails != null ? Trails.SolidCount : 0;

        /// <summary>Most chips thrown per second, after fitting the budget to the chip pool.</summary>
        public float ChipsPerSecond => Debris != null ? Debris.ChipsPerSecond : 0f;

        /// <summary>True while the start-up warm-up runs: from the scene's first asteroid for about half a second.</summary>
        public bool IsWarmingUp => warmUp != null && warmUp.IsRunning;

        /// <summary>True once the start-up warm-up has run to the end.</summary>
        public bool HasWarmedUp => warmUp != null && warmUp.HasStarted && !warmUp.IsRunning;

        /// <summary>Main-thread time of the last frame's trail work, in milliseconds. Part of <see cref="LastFrameMilliseconds"/>.</summary>
        public float TrailMilliseconds { get; private set; }

        /// <summary>Main-thread time of the last frame's chip throwing, in milliseconds. Part of <see cref="LastFrameMilliseconds"/>.</summary>
        public float ChipMilliseconds { get; private set; }

        /// <summary>Main-thread time of the last frame's destruction work, trails and chips included, in milliseconds.</summary>
        public float LastFrameMilliseconds { get; private set; }

        /// <summary>Worst main-thread time since the last <see cref="ResetStats"/>, in milliseconds.</summary>
        public float PeakFrameMilliseconds { get; private set; }

        internal int ChunksPerFrame => chunksPerFrame;
        internal int UrgentChunksPerFrame => urgentChunksPerFrame;
        internal int CollidersPerFrame => collidersPerFrame;
        internal int CutsPerFrame => cutsPerFrame;
        internal float HalfDetailStart => halfDetailStart;
        internal float QuarterDetailStart => quarterDetailStart;
        internal float DetailFadeTime => detailFadeTime;
        internal int MinPieceVoxels => minPieceVoxels;
        internal int MaxFreePieces => maxFreePieces;
        internal float PieceRange => pieceRange;
        internal float TrailRange => trailRange;
        internal float TrailPhysicsRange => trailPhysicsRange;
        internal int MaxSolidTrailRocks => maxSolidTrailRocks;
        internal float SolidRockMassPerTonne => solidRockMassPerTonne;
        internal float ShipKick => shipKick;
        internal Material TrailMaterial => trailMaterial;
        internal DebrisParticles Debris { get; private set; }
        internal EffectPool Effects { get; private set; }
        internal TrailRocks Trails { get; private set; }

        /// <summary>Builds a new voxel asteroid at the root of the scene. It appears over the next few frames.</summary>
        /// <param name="diameter">Mean diameter, in metres.</param>
        /// <param name="seed">The same seed always builds the same rock.</param>
        public VoxelBody SpawnAsteroid(AsteroidDefinition definition, float diameter, uint seed, Vector3 position, Quaternion rotation)
        {
            if (definition == null)
            {
                Debug.LogError("Cannot spawn an asteroid without an asteroid type.", this);
                return null;
            }

            var holder = new GameObject($"Voxel Asteroid ({definition.DisplayName})");
            holder.transform.SetPositionAndRotation(position, rotation);
            var body = holder.AddComponent<VoxelBody>();
            Generate(body, definition, diameter, seed);
            return body;
        }

        /// <summary>Removes every free piece, every chip in the air and every trail rock. Trails grow back.</summary>
        public void ClearDebris()
        {
            if (scheduler == null)
                return;

            foreach (VoxelBody body in scheduler.Bodies)
            {
                if (body != null && body.IsPiece)
                    scheduler.QueueRelease(body);
            }

            Debris?.Clear();
            Trails?.Clear();
        }

        /// <summary>Breaks a body into a burst of chips and removes it.</summary>
        public void Crumble(VoxelBody body)
        {
            if (scheduler != null && body != null)
                scheduler.Crumble(body);
        }

        public void ResetStats()
        {
            PeakFrameMilliseconds = 0f;
            removedByMaterial.Clear();
            scheduler?.ResetPeaks();
        }

        /// <summary>Names of the stages timed by <see cref="StageMilliseconds"/>.</summary>
        public static IReadOnlyList<string> StageNames => VoxelScheduler.StageNames;

        /// <summary>Main-thread time of one stage of the last frame's work, in milliseconds.</summary>
        public float StageMilliseconds(int stage)
        {
            return scheduler != null ? scheduler.StageMilliseconds(stage) : 0f;
        }

        /// <summary>Worst main-thread time of one stage since <see cref="ResetStats"/>, in milliseconds.</summary>
        public float PeakStageMilliseconds(int stage)
        {
            return scheduler != null ? scheduler.PeakStageMilliseconds(stage) : 0f;
        }

        internal void Generate(VoxelBody body, AsteroidDefinition definition, float diameter, uint seed)
        {
            Register(body);
            scheduler.Generate(body, definition, diameter, seed);

            // The first rock of the scene starts the warm-up, with its own type.
            if (warmUpOnLoad && !warmUp.HasStarted)
                warmUp.Begin(definition);
        }

        internal void Register(VoxelBody body)
        {
            scheduler?.Register(body);
        }

        internal void Unregister(VoxelBody body)
        {
            scheduler?.Unregister(body);
        }

        internal void QueueRelease(VoxelBody body)
        {
            scheduler?.QueueRelease(body);
        }

        /// <summary>Finishes the running jobs that use this body's grid. Other bodies' work carries on.</summary>
        internal void CompleteJobsFor(VoxelBody body)
        {
            scheduler?.CompleteJobsFor(body);
        }

        /// <summary>Fills the mesh pool for a full batch of urgent chunks, and makes the rock material's cross-fade copy.</summary>
        internal void PrewarmRendering(Material surface)
        {
            if (scheduler == null)
                return;

            scheduler.PrewarmMeshes(urgentChunksPerFrame);
            scheduler.PrewarmFade(surface);
        }

        /// <summary>Adds a new piece to the pool with spare chunk objects, so early breaks build no objects mid-frame.</summary>
        internal void PrewarmPiece(int chunks, Material material)
        {
            VoxelBody piece = CreatePiece();
            piece.PrewarmChunks(chunks, material);
            RetireBody(piece);
        }

        internal VoxelBody AcquirePiece()
        {
            if (piecePool.Count == 0)
                return CreatePiece();

            VoxelBody piece = piecePool[piecePool.Count - 1];
            piecePool.RemoveAt(piecePool.Count - 1);
            piece.transform.SetParent(null, false);
            piece.gameObject.SetActive(true);
            return piece;
        }

        private VoxelBody CreatePiece()
        {
            // The rigidbody comes first, so the kit's receivers on the chunks take the piece as their root.
            var holder = new GameObject($"Asteroid Piece {++pieceCount}");
            var rigidbody = holder.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            VoxelBody piece = holder.AddComponent<VoxelBody>();
            piece.IsPooled = true;
            return piece;
        }

        /// <summary>Takes a released body out of the world: pieces go back to the pool, the rest are destroyed.</summary>
        internal void RetireBody(VoxelBody body)
        {
            if (!body.IsPooled)
            {
                Destroy(body.gameObject);
                return;
            }

            body.gameObject.SetActive(false);
            body.transform.SetParent(poolRoot, false);
            piecePool.Add(body);
        }

        internal void RecordYield(in VoxelYield yield)
        {
            AsteroidDefinition definition = yield.Body != null ? yield.Body.Definition : null;
            if (definition != null)
            {
                for (int slot = 0; slot < definition.SlotCount; ++slot)
                {
                    VoxelMaterialDefinition material = definition.GetMaterial(slot);
                    float volume = yield[slot];
                    if (material == null || volume <= 0f)
                        continue;

                    removedByMaterial.TryGetValue(material, out float total);
                    removedByMaterial[material] = total + volume;
                }
            }

            MaterialRemoved?.Invoke(yield);
        }

        internal void RaiseBodySplit(VoxelBody body, VoxelBody piece)
        {
            BodySplit?.Invoke(body, piece);
        }

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Debug.LogWarning($"A second Destruction System on '{name}' was disabled. Keep one per scene.", this);
                enabled = false;
                return;
            }

            scheduler = new VoxelScheduler(this);
            warmUp = new DestructionWarmUp(this);
            poolRoot = new GameObject(PoolName).transform;
            poolRoot.SetParent(transform, false);
            Effects = new EffectPool(transform);
            Debris = new DebrisParticles(transform, ResolveChipMaterial(), maxChips, chipsPerCubicMetre, maxChipsPerCut, maxChipsPerSecond, chipSize);
            Trails = new TrailRocks(this, transform, maxTrailRocks);
        }

        private void OnEnable()
        {
            if (scheduler != null)
                Active = this;
        }

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        private void OnDestroy()
        {
            Trails?.Dispose();
            Trails = null;
            scheduler?.Dispose();
            scheduler = null;
        }

        // Runs early (execution order -50): chips cut last frame fly as their craters appear, and the trail job then
        // works alongside the game's own scripts.
        private void Update()
        {
            using (ChipsMarker.Auto())
            {
                timer.Restart();
                Debris?.Flush(Time.deltaTime);
                timer.Stop();
            }

            ChipMilliseconds = (float)timer.Elapsed.TotalMilliseconds;
            using (TrailsMarker.Auto())
            {
                timer.Restart();
                Trails?.BeginFrame(Time.deltaTime);
                timer.Stop();
            }

            trailStartMilliseconds = (float)timer.Elapsed.TotalMilliseconds;
        }

        private void LateUpdate()
        {
            if (scheduler == null)
                return;

            using (TickMarker.Auto())
            {
                timer.Restart();
                warmUp.Update();
                scheduler.Tick();
                timer.Stop();
            }

            float tick = (float)timer.Elapsed.TotalMilliseconds;
            using (TrailsMarker.Auto())
            {
                timer.Restart();
                Trails?.EndFrame();
                timer.Stop();
            }

            TrailMilliseconds = trailStartMilliseconds + (float)timer.Elapsed.TotalMilliseconds;
            LastFrameMilliseconds = tick + TrailMilliseconds + ChipMilliseconds;
            PeakFrameMilliseconds = Mathf.Max(PeakFrameMilliseconds, LastFrameMilliseconds);
        }

        private void FixedUpdate()
        {
            scheduler?.MoveBodies(Time.fixedDeltaTime);
        }

        private Material ResolveChipMaterial()
        {
            if (chipMaterial != null)
                return chipMaterial;

            Shader shader = Shader.Find("Particles/Standard Unlit");
            return shader != null ? new Material(shader) { name = "Rock Chips (Runtime)" } : null;
        }
    }
}
