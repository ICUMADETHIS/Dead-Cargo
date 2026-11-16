using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DeadCargo.Destruction.Voxels
{
    /// <summary>
    /// A rock made of voxels that weapons and tools can carve. Holds the voxel grid and the chunk objects that draw it
    /// and collide; the <see cref="DestructionSystem"/> does all the work (generating, meshing, cooking colliders,
    /// finding pieces that break away) in batched jobs. A piece cut free becomes a body of its own that drifts.
    /// The body's scale must stay 1: carving works in metres.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoxelBody : MonoBehaviour, IDestructible
    {
        /// <summary>Samples per axis a generated body may use. Larger rocks use larger voxels to stay within it.</summary>
        public const int MaxGridSize = 192;

        /// <summary>Empty samples kept round the rock on every side, so its surface never meets the grid's edge.</summary>
        internal const int Margin = 4;

        /// <summary>Most cuts that may wait on one rock. Only extreme fire reaches it; cuts past it are dropped.</summary>
        public const int MaxPendingCuts = 512;

        private static bool cutLimitReported;

        internal NativeArray<sbyte> Density;
        internal NativeArray<byte> Material;
        internal NativeArray<float4> Palette;
        internal int3 GridSize;
        internal float3 GridOrigin;
        internal int3 ChunkCount;
        internal VoxelChunk[] Chunks = new VoxelChunk[0];

        /// <summary>Per chunk: not queued, queued in the background, or queued urgently. See <see cref="VoxelScheduler"/>.</summary>
        internal byte[] ChunkQueue = new byte[0];

        /// <summary>Rises every time the grid is rebuilt, so work queued for an older grid is dropped.</summary>
        internal int Version;

        /// <summary>Rock per slice, written by the generation job.</summary>
        internal NativeArray<float> SliceVolume;
        internal readonly List<CarveCommand> PendingCarves = new List<CarveCommand>();

        internal JobHandle Generation;
        internal bool IsGenerating;

        /// <summary>A job is moving pieces out of this grid, or into it. Cuts, meshing and searches wait for it.</summary>
        internal JobHandle Extraction;
        internal bool IsBusy;

        /// <summary>Chunks of this body are being meshed; the grid is read by a job and cuts wait for it.</summary>
        internal bool InMeshBatch;
        internal int PendingMeshes;
        internal bool HasBeenMeshed;
        internal bool ReleaseRequested;
        internal bool IslandCheckQueued;
        internal bool HasSplitRegion;
        internal int3 SplitRegionMin;
        internal int3 SplitRegionMax;
        internal Vector3 LastCutPoint;
        internal Vector3 Velocity;
        internal Vector3 Spin;
        internal float FreedTime;
        internal bool IsPooled;

        private static readonly int TriplanarOffsetId = Shader.PropertyToID("_TriplanarOffset");
        private static MaterialPropertyBlock propertyBlock;

        private readonly List<VoxelChunk> spareChunks = new List<VoxelChunk>();
        private Rigidbody physicsBody;

        /// <summary>
        /// Where this body's origin sat in the asteroid it came from, in metres. The rock texture is projected from
        /// there, so a piece that breaks away keeps exactly the pattern it had.
        /// </summary>
        internal float3 TextureOffset { get; private set; }

        public AsteroidDefinition Definition { get; private set; }

        /// <summary>Spacing of the voxels, in metres.</summary>
        public float VoxelSize { get; private set; }

        public bool IsAllocated => Density.IsCreated;

        /// <summary>True once the rock is built and every chunk is meshed.</summary>
        public bool IsReady => IsAllocated && !IsGenerating && !IsBusy && HasBeenMeshed && PendingMeshes == 0 && !ReleaseRequested;

        /// <summary>True for a piece that broke away and drifts on its own.</summary>
        public bool IsFree { get; private set; }

        /// <summary>Rock left, in cubic metres.</summary>
        public float Volume { get; internal set; }

        /// <summary>Rock the body started with, in cubic metres.</summary>
        public float InitialVolume { get; internal set; }

        public int TriangleCount { get; internal set; }

        /// <summary>Chunks that currently have a surface.</summary>
        public int SurfaceChunks { get; internal set; }

        /// <summary>Memory held by the voxel grid, in bytes.</summary>
        public long MemoryBytes => IsAllocated ? (long)Density.Length * 2 : 0;

        /// <summary>World velocity of a free piece, in metres per second. Zero for a body held in place.</summary>
        public Vector3 LinearVelocity => IsFree ? Velocity : Vector3.zero;

        internal Rigidbody PhysicsBody => physicsBody;

        /// <summary>Raised for every cut that removes rock, with what was removed by material.</summary>
        public event Action<VoxelYield> MaterialRemoved;

        /// <summary>Raised when a piece breaks away, with this body and the new piece.</summary>
        public event Action<VoxelBody, VoxelBody> Split;

        /// <summary>Raised when the last of the rock has gone, just before the body is released.</summary>
        public event Action<VoxelBody> Depleted;

        /// <summary>Builds a new asteroid of the given type in this body, replacing whatever it held.</summary>
        /// <param name="diameter">Mean diameter, in metres.</param>
        /// <param name="seed">The same seed always builds the same rock.</param>
        public void Generate(AsteroidDefinition definition, float diameter, uint seed)
        {
            DestructionSystem system = DestructionSystem.Active;
            if (system == null)
            {
                Debug.LogError($"'{name}' cannot build its asteroid: there is no Destruction System in the scene. " +
                               "Run Set Up Sector from the God Menu Project tab.", this);
                return;
            }

            system.Generate(this, definition, diameter, seed);
        }

        public void ApplyDamage(in DamageEvent damage)
        {
            if (Definition == null || damage.Amount <= 0f && damage.Radius <= 0f)
                return;

            float radius = Definition.CraterRadius(damage);

            // A hit digs in along its path, so craters face the shooter and go deeper than they are wide.
            bool hasDirection = damage.Direction.sqrMagnitude > 0.5f;
            Vector3 centre = hasDirection ? damage.Point + damage.Direction * (radius * 0.35f) : damage.Point;
            Vector3 normal = hasDirection ? -damage.Direction : (damage.Point - transform.position).normalized;
            Carve(centre, radius, damage.Kind, normal);
        }

        /// <summary>Cuts a sphere of rock out of the body. Takes effect in the next frame's batch.</summary>
        /// <param name="radius">In metres.</param>
        /// <returns>False when the body cannot be cut now.</returns>
        public bool Carve(Vector3 worldCentre, float radius, DamageKind kind)
        {
            return Carve(worldCentre, radius, kind, (worldCentre - transform.position).normalized);
        }

        /// <param name="normal">World direction the chips fly out along.</param>
        public bool Carve(Vector3 worldCentre, float radius, DamageKind kind, Vector3 normal)
        {
            if (!IsAllocated || ReleaseRequested || !(radius > 0f) || !HasRoomForCut())
                return false;

            float3 grid = WorldToGrid(worldCentre);
            PendingCarves.Add(new CarveCommand
            {
                Request = new CarveRequest
                {
                    Centre = grid,
                    Radius = radius / VoxelSize,
                    Roughness = Definition != null ? Definition.CraterRoughness : 0f,
                    NoiseOffset = math.frac(grid * 0.137f) * 97f
                },
                Kind = kind,
                Point = worldCentre,
                Normal = normal,
                Radius = radius
            });

            return true;
        }

        /// <summary>
        /// Saws a flat cut through the body: a disc of rock removed, which splits it when it goes all the way through.
        /// Takes effect in the next frame's batch.
        /// </summary>
        /// <param name="worldNormal">Facing of the cut.</param>
        /// <param name="thickness">Width of the cut, in metres.</param>
        /// <param name="radius">Reach of the cut from its centre, in metres.</param>
        public bool Cut(Vector3 worldCentre, Vector3 worldNormal, float thickness, float radius, DamageKind kind)
        {
            if (!IsAllocated || ReleaseRequested || !(thickness > 0f) || !(radius > 0f) || worldNormal.sqrMagnitude < 1e-6f ||
                !HasRoomForCut())
                return false;

            PendingCarves.Add(new CarveCommand
            {
                Request = new CarveRequest
                {
                    Shape = CarveShape.Slab,
                    Centre = WorldToGrid(worldCentre),
                    Normal = math.normalize((float3)transform.InverseTransformDirection(worldNormal)),
                    HalfThickness = thickness * 0.5f / VoxelSize,
                    Radius = radius / VoxelSize
                },
                Kind = kind,
                Point = worldCentre,
                Normal = worldNormal.normalized,
                Radius = thickness
            });

            return true;
        }

        private bool HasRoomForCut()
        {
            if (PendingCarves.Count < MaxPendingCuts)
                return true;

            if (!cutLimitReported)
            {
                cutLimitReported = true;
                Debug.LogWarning($"'{name}' has {MaxPendingCuts} cuts waiting; further hits are dropped until it catches up.", this);
            }

            return false;
        }

        /// <summary>Removes the body from the world. Pieces return to the pool; placed asteroids are destroyed.</summary>
        public void Release()
        {
            DestructionSystem system = DestructionSystem.Active;
            if (system != null)
                system.QueueRelease(this);
            else
                Destroy(gameObject);
        }

        /// <summary>A world position in grid coordinates (voxels).</summary>
        public float3 WorldToGrid(Vector3 world)
        {
            float3 local = transform.InverseTransformPoint(world);
            return (local - GridOrigin) / VoxelSize;
        }

        /// <summary>A grid position (voxels) in world space.</summary>
        public Vector3 GridToWorld(float3 grid)
        {
            return transform.TransformPoint(GridOrigin + grid * VoxelSize);
        }

        internal int3 ChunkMin(int index)
        {
            int x = index % ChunkCount.x;
            int yz = index / ChunkCount.x;
            return new int3(x, yz % ChunkCount.y, yz / ChunkCount.y) * SurfaceNets.ChunkSize;
        }

        internal int ChunkIndex(int3 chunk)
        {
            return chunk.x + ChunkCount.x * (chunk.y + ChunkCount.y * chunk.z);
        }

        /// <summary>Sets up an empty grid. The caller fills it and queues its chunks.</summary>
        internal void Allocate(AsteroidDefinition definition, int3 size, float voxelSize, float3 origin)
        {
            DisposeGrid();
            Definition = definition;
            VoxelSize = voxelSize;
            GridSize = size;
            GridOrigin = origin;

            int count = size.x * size.y * size.z;
            Density = new NativeArray<sbyte>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            Material = new NativeArray<byte>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            Palette = new NativeArray<float4>(VoxelEncoding.MaterialSlots, Allocator.Persistent);
            if (definition != null)
                definition.FillPalette(Palette);

            ChunkCount = SurfaceNets.ChunkCount(size);
            int chunks = ChunkCount.x * ChunkCount.y * ChunkCount.z;
            Chunks = new VoxelChunk[chunks];
            ChunkQueue = new byte[chunks];
            ++Version;

            PendingCarves.Clear();
            IsGenerating = false;
            IsBusy = false;
            InMeshBatch = false;
            PendingMeshes = 0;
            HasBeenMeshed = false;
            ReleaseRequested = false;
            IslandCheckQueued = false;
            HasSplitRegion = false;
            TriangleCount = 0;
            SurfaceChunks = 0;
            Volume = 0f;
            InitialVolume = 0f;
            IsFree = false;
            Velocity = Vector3.zero;
            Spin = Vector3.zero;
            TextureOffset = float3.zero;
        }

        internal VoxelChunk GetOrCreateChunk(int index)
        {
            VoxelChunk chunk = Chunks[index];
            if (chunk != null)
                return chunk;

            Material material = Definition != null ? Definition.SurfaceMaterial : null;
            if (spareChunks.Count > 0)
            {
                chunk = spareChunks[spareChunks.Count - 1];
                spareChunks.RemoveAt(spareChunks.Count - 1);
                chunk.SetMaterial(material);
                chunk.GameObject.SetActive(true);
            }
            else
            {
                chunk = new VoxelChunk(this, material);
            }

            ApplyTextureOffset(chunk);
            Chunks[index] = chunk;
            return chunk;
        }

        internal void SetTextureOffset(float3 offset)
        {
            TextureOffset = offset;
            foreach (VoxelChunk chunk in Chunks)
            {
                if (chunk != null && chunk.IsAlive)
                    ApplyTextureOffset(chunk);
            }
        }

        private void ApplyTextureOffset(VoxelChunk chunk)
        {
            if (math.all(TextureOffset == float3.zero))
            {
                chunk.Renderer.SetPropertyBlock(null);
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.SetVector(TriplanarOffsetId, new Vector4(TextureOffset.x, TextureOffset.y, TextureOffset.z, 0f));
            chunk.Renderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>Parks a chunk object for reuse. Its meshes must already be released.</summary>
        internal void RetireChunk(int index)
        {
            VoxelChunk chunk = Chunks[index];
            if (chunk == null)
                return;

            Chunks[index] = null;
            if (!chunk.IsAlive)
                return;

            chunk.GameObject.SetActive(false);
            spareChunks.Add(chunk);
        }

        internal void SetVisible(bool visible)
        {
            foreach (VoxelChunk chunk in Chunks)
            {
                if (chunk == null || !chunk.IsAlive)
                    continue;

                chunk.Renderer.enabled = visible && chunk.RenderMesh != null;
                chunk.Collider.enabled = visible && chunk.ColliderMesh != null;
            }
        }

        /// <summary>Lets the body drift as a free piece, moved kinematically so it never pushes back on its parent.</summary>
        internal void MakeFree(Vector3 velocity, Vector3 spin)
        {
            if (physicsBody == null)
                physicsBody = GetComponent<Rigidbody>();
            if (physicsBody == null)
                physicsBody = gameObject.AddComponent<Rigidbody>();

            physicsBody.isKinematic = true;
            physicsBody.useGravity = false;
            physicsBody.interpolation = RigidbodyInterpolation.Interpolate;

            // The pivot is already the centre of mass, and the piece is moved by hand: recomputing mass properties
            // every time a chunk's collider changes would be wasted work.
            physicsBody.automaticCenterOfMass = false;
            physicsBody.centerOfMass = Vector3.zero;
            physicsBody.automaticInertiaTensor = false;
            physicsBody.inertiaTensor = Vector3.one;
            physicsBody.inertiaTensorRotation = Quaternion.identity;
            physicsBody.position = transform.position;
            physicsBody.rotation = transform.rotation;

            IsFree = true;
            Velocity = velocity;
            Spin = spin;
            FreedTime = Time.time;
        }

        internal void RaiseMaterialRemoved(in VoxelYield yield)
        {
            MaterialRemoved?.Invoke(yield);
        }

        internal void RaiseSplit(VoxelBody piece)
        {
            Split?.Invoke(this, piece);
        }

        internal void RaiseDepleted()
        {
            Depleted?.Invoke(this);
        }

        internal void DisposeGrid()
        {
            if (Density.IsCreated)
                Density.Dispose();
            if (Material.IsCreated)
                Material.Dispose();
            if (Palette.IsCreated)
                Palette.Dispose();
            if (SliceVolume.IsCreated)
                SliceVolume.Dispose();
        }

        private void OnEnable()
        {
            DestructionSystem system = DestructionSystem.Active;
            if (system != null)
                system.Register(this);
        }

        private void OnDisable()
        {
            DestructionSystem system = DestructionSystem.Active;
            if (system != null)
                system.Unregister(this);
        }

        private void OnDestroy()
        {
            // Jobs read the grid through raw pointers, so none may still be running when it is freed.
            DestructionSystem system = DestructionSystem.Active;
            if (system != null)
                system.CompleteJobs();

            Generation.Complete();
            Extraction.Complete();
            DisposeGrid();
        }
    }
}
