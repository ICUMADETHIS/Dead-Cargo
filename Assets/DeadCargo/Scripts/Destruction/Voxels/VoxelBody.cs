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
    /// finding pieces that break away) in batched jobs. A piece cut free becomes a body of its own.
    /// <para>
    /// Every body moves as a real rock: it drifts, tumbles about its centre of mass with the wobble its shape gives it,
    /// and takes pushes from hits. Mass, centre and inertia follow every crater and lost piece exactly. Bodies are
    /// moved kinematically, so they push ships but are never pushed back by them.
    /// </para>
    /// The body's scale must stay 1: carving works in metres.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoxelBody : MonoBehaviour, IDestructible
    {
        /// <summary>Samples per axis a generated body may use. Larger rocks use larger voxels to stay within it.</summary>
        public const int MaxGridSize = 192;

        /// <summary>Most cuts that may wait on one rock. Only extreme fire reaches it; cuts past it are dropped.</summary>
        public const int MaxPendingCuts = 512;

        /// <summary>Fastest a rock may drift, in metres per second.</summary>
        public const float MaxSpeed = 100f;

        /// <summary>Fastest a rock may turn, in radians per second (about 230 degrees per second).</summary>
        public const float MaxSpin = 4f;

        /// <summary>Empty samples kept round the rock on every side, so its surface never meets the grid's edge.</summary>
        internal const int Margin = 4;

        // Narrowest crack gap, in voxels. Wide enough that no ragged face pinches it shut, so the parts always separate.
        private const float MinCrackVoxels = 2.5f;

        // Below this mass, in tonnes, a body has nothing left to move.
        private const float MinMass = 1e-3f;

        private static readonly float2[] ShareRanges = { new float2(0.04f, 0.10f), new float2(0.12f, 0.25f), new float2(0.30f, 0.50f) };

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
        internal NativeArray<MassMoments> SliceMoments;
        internal readonly List<CarveCommand> PendingCarves = new List<CarveCommand>();

        internal JobHandle Generation;
        internal bool IsGenerating;

        /// <summary>
        /// A job is writing this grid: moving pieces out of it or into it, or making a heavy cut. Cuts, meshing and
        /// searches wait for it.
        /// </summary>
        internal JobHandle GridJob;
        internal bool IsBusy;

        /// <summary>Trail rocks owed to this body's trail, carried between frames. See <see cref="TrailRocks"/>.</summary>
        internal float TrailCharge;

        /// <summary>World direction the body's trail streams out in: behind it, against its drift.</summary>
        internal Vector3 TrailBack;

        /// <summary>The body's slot in the trail owner table its trail rocks follow, or -1 before it first trails.</summary>
        internal int TrailSlot = -1;

        /// <summary>The body was within trail range last frame. Coming back into range tops its trail up.</summary>
        internal bool TrailInRange;

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
        internal bool IsPooled;

        /// <summary>The rock, as mass moments in grid coordinates. Kept exact through every cut and lost piece.</summary>
        internal MassMoments Moments;


        private readonly List<VoxelChunk> spareChunks = new List<VoxelChunk>();
        private Rigidbody physicsBody;

        private float mass;
        private float3 centreOfMass;
        private float3x3 inertia;
        private float3x3 inverseInertia;
        private Vector3 velocity;
        private Vector3 angularMomentum;

        // Spin asked for before the rock's mass is known; it takes effect once the rock is built.
        private Vector3 pendingSpin;

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

        /// <summary>True once the body has been told to leave the world. It is gone by the next frame.</summary>
        public bool IsReleased => ReleaseRequested;

        /// <summary>True for a piece that broke away from a larger rock. Pieces count against the free-piece budget.</summary>
        public bool IsPiece { get; private set; }

        /// <summary>
        /// True for the hidden rock the system cuts once at load, and its pieces, so the first real hit never pays for
        /// code running for the first time. It raises no system events and is gone within a second; tools skip it.
        /// </summary>
        public bool IsWarmUp { get; private set; }

        /// <summary>
        /// Mesh detail: 0 for full, 1 for the half detail and 2 for the quarter detail distant rock uses. Set by the
        /// scheduler from distance.
        /// </summary>
        public int DetailLevel { get; private set; }

        /// <summary>Rock left, in cubic metres.</summary>
        public float Volume { get; internal set; }

        /// <summary>Diameter of a ball holding the same rock, in metres.</summary>
        public float EquivalentDiameter => 2f * Mathf.Pow(Mathf.Max(Volume, 0f) * (3f / (4f * Mathf.PI)), 1f / 3f);

        /// <summary>Rock the body started with, in cubic metres.</summary>
        public float InitialVolume { get; internal set; }

        public int TriangleCount { get; internal set; }

        /// <summary>Chunks that currently have a surface.</summary>
        public int SurfaceChunks { get; internal set; }

        /// <summary>Memory held by the voxel grid, in bytes.</summary>
        public long MemoryBytes => IsAllocated ? (long)Density.Length * 2 : 0;

        /// <summary>Mass of the rock, in tonnes. Zero until it is built.</summary>
        public float Mass => mass;

        /// <summary>World position of the centre of mass, which the rock turns about.</summary>
        public Vector3 CentreOfMass => PosePosition + PoseRotation * (Vector3)centreOfMass;

        /// <summary>World velocity of the centre of mass, in metres per second.</summary>
        public Vector3 LinearVelocity => velocity;

        /// <summary>World angular velocity, in radians per second. Changes on its own as a lopsided rock wobbles.</summary>
        public Vector3 AngularVelocity => SpinFor(angularMomentum, PoseRotation);

        internal Rigidbody PhysicsBody => physicsBody;

        /// <summary>The rotation physics knows, which colliders and hits use.</summary>
        internal Quaternion WorldRotation => PoseRotation;

        // The pose physics knows: colliders, and so every hit, sit here, while the transform may be interpolated.
        private Vector3 PosePosition => physicsBody != null ? physicsBody.position : transform.position;
        private Quaternion PoseRotation => physicsBody != null ? physicsBody.rotation : transform.rotation;

        /// <summary>Raised for every cut that removes rock, with what was removed by material.</summary>
        public event Action<VoxelYield> MaterialRemoved;

        /// <summary>Raised when a piece breaks away, with this body and the new piece.</summary>
        public event Action<VoxelBody, VoxelBody> Split;

        /// <summary>Raised when the last of the rock has gone, just before the body is released.</summary>
        public event Action<VoxelBody> Depleted;

        /// <summary>Builds a new asteroid of the given type in this body, replacing whatever it held.</summary>
        /// <param name="diameter">Mean diameter, in metres.</param>
        /// <param name="seed">The same seed always builds the same rock, with the same drift and tumble.</param>
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

            // Hits push the rock where they land: enough to shove a boulder, barely noticed by a large asteroid.
            float push = Definition.Push(damage);
            if (push > 0f)
            {
                Vector3 along = hasDirection ? damage.Direction : (CentreOfMass - damage.Point).normalized;
                AddImpulse(along * push, damage.Point);
            }
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
                    Normal = math.normalize((float3)(Quaternion.Inverse(PoseRotation) * worldNormal)),
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

        /// <summary>
        /// Cracks a chunk off the rock along a ragged break, the way rock splits. The chunk is the part beyond a crack
        /// facing <paramref name="worldDirection"/>, placed so it holds <paramref name="share"/> of the rock. The chunk
        /// and the rest push apart, each as much less as it is heavier. Takes effect in the next frame's batch.
        /// </summary>
        /// <param name="worldDirection">From the rock towards the chunk that breaks off.</param>
        /// <param name="share">Share of the rock the chunk holds, 0 to 1.</param>
        public bool Fracture(Vector3 worldDirection, float share, DamageKind kind)
        {
            if (!IsAllocated || ReleaseRequested || !(share > 0f) || worldDirection.sqrMagnitude < 1e-6f || !HasRoomForCut())
                return false;

            float width = Definition != null ? Definition.CrackWidth : 1f;
            Vector3 direction = worldDirection.normalized;
            PendingCarves.Add(new CarveCommand
            {
                Request = new CarveRequest
                {
                    Shape = CarveShape.Fracture,
                    Normal = math.normalize((float3)(Quaternion.Inverse(PoseRotation) * direction)),
                    Share = math.saturate(share),
                    HalfThickness = math.max(width / VoxelSize, MinCrackVoxels) * 0.5f,
                    Roughness = Definition != null ? Definition.CrackRoughness : 0.8f,
                    NoiseOffset = UnityEngine.Random.insideUnitSphere * 100f
                },
                Kind = kind,

                // Where the crack lands is only known once it is made; the scheduler fills these in then.
                Point = CentreOfMass,
                Normal = direction,
                Radius = width
            });

            return true;
        }

        /// <summary>Cracks off a chunk of the given size, anywhere on the rock: the direction and exact share are random.</summary>
        public bool Fracture(FractureSize size, DamageKind kind)
        {
            Vector2 range = FractureShareRange(size);
            return Fracture(UnityEngine.Random.onUnitSphere, UnityEngine.Random.Range(range.x, range.y), kind);
        }

        /// <summary>The share of the rock, lowest and highest, a chunk of the given size holds.</summary>
        public static Vector2 FractureShareRange(FractureSize size)
        {
            float2 range = ShareRanges[math.clamp((int)size, 0, ShareRanges.Length - 1)];
            return new Vector2(range.x, range.y);
        }

        /// <summary>Sets how the rock moves. Asked before the rock is built, it takes effect once it is.</summary>
        /// <param name="linearVelocity">World velocity of the centre of mass, in metres per second.</param>
        /// <param name="angularVelocity">World angular velocity, in radians per second.</param>
        public void SetMotion(Vector3 linearVelocity, Vector3 angularVelocity)
        {
            velocity = Vector3.ClampMagnitude(linearVelocity, MaxSpeed);
            if (mass > 0f)
            {
                angularMomentum = WorldInertiaTimes(angularVelocity);
                LimitSpin();
            }
            else
            {
                angularMomentum = Vector3.zero;
                pendingSpin = angularVelocity;
            }
        }

        /// <summary>Pushes the rock at a world point. Off-centre pushes also set it turning.</summary>
        /// <param name="impulse">In tonne metres per second: a 10 tonne rock pushed with 10 moves 1 m/s faster.</param>
        public void AddImpulse(Vector3 impulse, Vector3 worldPoint)
        {
            if (!(mass > 0f))
                return;

            velocity = Vector3.ClampMagnitude(velocity + impulse / mass, MaxSpeed);
            angularMomentum += Vector3.Cross(worldPoint - CentreOfMass, impulse);
            LimitSpin();
        }

        /// <summary>Twists the rock about its centre of mass.</summary>
        /// <param name="angularImpulse">In tonne square metres per second.</param>
        public void AddAngularImpulse(Vector3 angularImpulse)
        {
            if (!(mass > 0f))
                return;

            angularMomentum += angularImpulse;
            LimitSpin();
        }

        /// <summary>The angular momentum that would turn the rock at the given world angular velocity.</summary>
        public Vector3 AngularMomentumFor(Vector3 angularVelocity)
        {
            return mass > 0f ? WorldInertiaTimes(angularVelocity) : Vector3.zero;
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
            float3 local = Quaternion.Inverse(PoseRotation) * (world - PosePosition);
            return (local - GridOrigin) / VoxelSize;
        }

        /// <summary>A grid position (voxels) in world space.</summary>
        public Vector3 GridToWorld(float3 grid)
        {
            return PosePosition + PoseRotation * (Vector3)(GridOrigin + grid * VoxelSize);
        }

        /// <summary>A body-local position, in metres, in world space.</summary>
        internal Vector3 LocalToWorld(float3 local)
        {
            return PosePosition + PoseRotation * (Vector3)local;
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
            IsPiece = false;
            IsWarmUp = false;
            TextureOffset = float3.zero;
            TrailCharge = 0f;
            TrailBack = Vector3.zero;
            TrailSlot = -1;
            TrailInRange = false;
            SetDetailLevel(0);

            Moments = default;
            mass = 0f;
            centreOfMass = float3.zero;
            inertia = float3x3.zero;
            inverseInertia = float3x3.zero;
            velocity = Vector3.zero;
            angularMomentum = Vector3.zero;
            pendingSpin = Vector3.zero;
        }

        /// <summary>
        /// Works out mass, centre of mass and inertia again from <see cref="Moments"/>, after rock was added or lost.
        /// The rock keeps turning as it was, and its centre's velocity follows the centre to where it now sits.
        /// </summary>
        internal void UpdateMassProperties()
        {
            bool hadMass = mass > 0f;
            Vector3 spin = hadMass ? AngularVelocity : pendingSpin;
            Vector3 oldCentre = CentreOfMass;

            double density = Definition != null ? Definition.Density : 2.5;
            MassMoments metres = Moments.Transformed(VoxelSize, GridOrigin);
            mass = (float)(metres.Volume * density);
            if (!(mass > MinMass))
            {
                mass = 0f;
                inertia = float3x3.zero;
                inverseInertia = float3x3.zero;
                angularMomentum = Vector3.zero;
                return;
            }

            centreOfMass = (float3)metres.Centre;
            double3x3 tensor = metres.InertiaAboutCentre(VoxelSize) * density;
            inertia = (float3x3)tensor;
            inverseInertia = (float3x3)math.inverse(tensor);

            if (hadMass)
                velocity += Vector3.Cross(spin, CentreOfMass - oldCentre);

            angularMomentum = WorldInertiaTimes(spin);
            pendingSpin = Vector3.zero;
            LimitSpin();
        }

        /// <summary>Moves the rock one physics step along its drift and tumble.</summary>
        internal void Integrate(float deltaTime)
        {
            if (!(mass > 0f) || physicsBody == null)
                return;

            bool drifting = velocity.sqrMagnitude > 0f;
            bool turning = angularMomentum.sqrMagnitude > 0f;
            if (!drifting && !turning)
                return;

            Quaternion rotation = physicsBody.rotation;
            Vector3 centre = physicsBody.position + rotation * (Vector3)centreOfMass;
            if (turning)
            {
                // A free rock keeps its angular momentum, not its spin axis: a lopsided one wobbles about it. The spin
                // is worked out again halfway through the step, which keeps that wobble steady over long runs.
                Vector3 start = SpinFor(angularMomentum, rotation);
                Quaternion halfway = Turn(start, deltaTime * 0.5f) * rotation;
                Vector3 middle = SpinFor(angularMomentum, halfway);
                rotation = Turn(middle, deltaTime) * rotation;
                rotation.Normalize();
            }

            centre += velocity * deltaTime;
            physicsBody.MovePosition(centre - rotation * (Vector3)centreOfMass);
            physicsBody.MoveRotation(rotation);
        }

        /// <summary>Puts the body at a pose at once, with no motion in between.</summary>
        internal void PlaceAt(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (physicsBody == null)
                return;

            physicsBody.position = position;
            physicsBody.rotation = rotation;
        }

        /// <param name="isWarmUp">The piece came off the warm-up rock and stays out of the game like it.</param>
        internal void MarkAsPiece(bool isWarmUp)
        {
            IsPiece = true;
            IsWarmUp = isWarmUp;
        }

        /// <summary>Marks a freshly built body as the warm-up rock. Building it again clears the mark.</summary>
        internal void MarkAsWarmUp()
        {
            IsWarmUp = true;
        }

        /// <summary>
        /// Makes spare chunk objects up front, so this body's first breaks reuse them instead of building objects
        /// mid-frame. Only for pooled pieces, while they sit in the pool.
        /// </summary>
        internal void PrewarmChunks(int count, Material material)
        {
            for (int i = spareChunks.Count; i < count; ++i)
            {
                var chunk = new VoxelChunk(this, material);
                chunk.GameObject.SetActive(false);
                spareChunks.Add(chunk);
            }
        }

        /// <summary>
        /// Sets the mesh detail. The caller queues the chunks again, and they cross-fade to the new detail. Motion
        /// smoothing stays on at every level: switching it back on would blend from a pose long stale and flash the rock
        /// back to where it was.
        /// </summary>
        internal void SetDetailLevel(int level)
        {
            DetailLevel = level;
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

        /// <summary>Sets a chunk's per-renderer look: this body's texture offset, and its cross-fade while one runs.</summary>
        internal void ApplyTextureOffset(VoxelChunk chunk)
        {
            chunk.ApplyLook(new Vector4(TextureOffset.x, TextureOffset.y, TextureOffset.z, 0f));
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
                chunk.SetGhostVisible(visible && chunk.FadeMesh != null);
            }
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
            if (SliceMoments.IsCreated)
                SliceMoments.Dispose();
        }

        // World angular velocity for an angular momentum, at a rotation: the inertia is fixed to the rock, so the same
        // momentum turns it differently as it tumbles.
        private Vector3 SpinFor(Vector3 momentum, Quaternion rotation)
        {
            if (!(mass > 0f))
                return Vector3.zero;

            float3 local = math.mul(inverseInertia, (float3)(Quaternion.Inverse(rotation) * momentum));
            return rotation * (Vector3)local;
        }

        private Vector3 WorldInertiaTimes(Vector3 spin)
        {
            Quaternion rotation = PoseRotation;
            float3 local = math.mul(inertia, (float3)(Quaternion.Inverse(rotation) * spin));
            return rotation * (Vector3)local;
        }

        private void LimitSpin()
        {
            float spin = AngularVelocity.magnitude;
            if (spin > MaxSpin)
                angularMomentum *= MaxSpin / spin;
        }

        private static Quaternion Turn(Vector3 spin, float time)
        {
            float speed = spin.magnitude;
            return speed > 1e-6f ? Quaternion.AngleAxis(speed * time * Mathf.Rad2Deg, spin / speed) : Quaternion.identity;
        }

        private void Awake()
        {
            physicsBody = GetComponent<Rigidbody>();
            if (physicsBody == null)
                physicsBody = gameObject.AddComponent<Rigidbody>();

            // Concave mesh colliders need a kinematic body, so the rock is moved by hand from its own mass and inertia.
            // Unity's are pinned, so it never recomputes them as the chunk colliders change.
            physicsBody.isKinematic = true;
            physicsBody.useGravity = false;
            physicsBody.interpolation = RigidbodyInterpolation.Interpolate;
            physicsBody.automaticCenterOfMass = false;
            physicsBody.centerOfMass = Vector3.zero;
            physicsBody.automaticInertiaTensor = false;
            physicsBody.inertiaTensor = Vector3.one;
            physicsBody.inertiaTensorRotation = Quaternion.identity;
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
            // Jobs read the grid through raw pointers, so none that uses it may still be running when it is freed. A
            // released body has already freed its grid and waits for nothing.
            DestructionSystem system = DestructionSystem.Active;
            if (system != null && IsAllocated)
                system.CompleteJobsFor(this);

            Generation.Complete();
            GridJob.Complete();
            DisposeGrid();
        }
    }
}
