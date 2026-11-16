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
    /// The scene service behind everything destructible. Runs the voxel pipeline for all bodies once a frame, keeps the
    /// pools (pieces, meshes, effects), throws rock chips, and reports what was removed so mining and other systems can
    /// react. Lives in the Systems folder; one per scene.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class DestructionSystem : MonoBehaviour
    {
        private const string PoolName = "Piece Pool";

        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("DeadCargo.Destruction.Tick");

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

        [Tooltip("Most chips thrown in one frame, however many cuts land in it.")]
        [SerializeField, Range(1, 512)] private int maxChipsPerFrame = 96;

        [Tooltip("Size of a chip from a one-metre cut, in metres.")]
        [SerializeField, Min(0.01f)] private float chipSize = 0.35f;

        private readonly Dictionary<VoxelMaterialDefinition, float> removedByMaterial = new Dictionary<VoxelMaterialDefinition, float>();
        private readonly List<VoxelBody> piecePool = new List<VoxelBody>();
        private readonly Stopwatch timer = new Stopwatch();
        private VoxelScheduler scheduler;
        private Transform poolRoot;
        private int pieceCount;

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
        public long ScratchBytes => scheduler != null ? scheduler.ScratchBytes : 0;
        public int ChipCount => Debris != null ? Debris.Count : 0;

        /// <summary>Main-thread time of the last frame's destruction work, in milliseconds.</summary>
        public float LastFrameMilliseconds { get; private set; }

        /// <summary>Worst main-thread time since the last <see cref="ResetStats"/>, in milliseconds.</summary>
        public float PeakFrameMilliseconds { get; private set; }

        internal int ChunksPerFrame => chunksPerFrame;
        internal int UrgentChunksPerFrame => urgentChunksPerFrame;
        internal int CollidersPerFrame => collidersPerFrame;
        internal int CutsPerFrame => cutsPerFrame;
        internal int MinPieceVoxels => minPieceVoxels;
        internal int MaxFreePieces => maxFreePieces;
        internal float PieceRange => pieceRange;
        internal DebrisParticles Debris { get; private set; }
        internal EffectPool Effects { get; private set; }

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

        /// <summary>Removes every free piece and every chip in the air.</summary>
        public void ClearDebris()
        {
            if (scheduler == null)
                return;

            foreach (VoxelBody body in scheduler.Bodies)
            {
                if (body != null && body.IsFree)
                    scheduler.QueueRelease(body);
            }

            Debris?.Clear();
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

        internal void CompleteJobs()
        {
            scheduler?.CompleteJobs();
        }

        internal VoxelBody AcquirePiece()
        {
            VoxelBody piece;
            if (piecePool.Count > 0)
            {
                piece = piecePool[piecePool.Count - 1];
                piecePool.RemoveAt(piecePool.Count - 1);
                piece.transform.SetParent(null, false);
                piece.gameObject.SetActive(true);
            }
            else
            {
                // The rigidbody comes first, so the kit's receivers on the chunks take the piece as their root.
                var holder = new GameObject($"Asteroid Piece {++pieceCount}");
                var rigidbody = holder.AddComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                piece = holder.AddComponent<VoxelBody>();
                piece.IsPooled = true;
            }

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
            poolRoot = new GameObject(PoolName).transform;
            poolRoot.SetParent(transform, false);
            Effects = new EffectPool(transform);
            Debris = new DebrisParticles(transform, ResolveChipMaterial(), maxChips, chipsPerCubicMetre, maxChipsPerCut, maxChipsPerFrame, chipSize);
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
            scheduler?.Dispose();
            scheduler = null;
        }

        private void LateUpdate()
        {
            if (scheduler == null)
                return;

            using (TickMarker.Auto())
            {
                timer.Restart();
                scheduler.Tick();
                timer.Stop();
            }

            LastFrameMilliseconds = (float)timer.Elapsed.TotalMilliseconds;
            PeakFrameMilliseconds = Mathf.Max(PeakFrameMilliseconds, LastFrameMilliseconds);
        }

        private void FixedUpdate()
        {
            scheduler?.MoveFreeBodies(Time.fixedDeltaTime);
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
