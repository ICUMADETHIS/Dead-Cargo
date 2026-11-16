using System;
using System.Collections.Generic;
using DeadCargo.Destruction.Voxels;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Random = Unity.Mathematics.Random;

namespace DeadCargo.Destruction
{
    /// <summary>
    /// Trails of small rocks behind large asteroids, like rubble shed along a rock's path. Every trail rock in the scene
    /// lives in one native array, is moved by one Burst job and is drawn in instanced batches of up to 1023, with no
    /// object per rock.
    /// <para>
    /// A trail rock rides with its asteroid and slides slowly back along the trail, so the trail follows the asteroid
    /// wherever it drifts and swings round behind it when it turns or is pushed (see <see cref="TrailRockJob"/>).
    /// Only rocks near the camera are solid: each borrows a pooled <see cref="TrailRockProxy"/>, so ships hit them and
    /// knock them away, and weapons shatter them into chips. A rock knocked hard enough breaks from its trail and flies
    /// on. Further out, rocks cost almost nothing.
    /// </para>
    /// The job is scheduled early in the frame and finished in late update, so it runs alongside the game's own scripts.
    /// </summary>
    internal sealed class TrailRocks : IDisposable
    {
        private const int BatchSize = 1023;
        private const int JobBatch = 64;
        private const int InitialOwnerSlots = 32;
        private const float GrowTime = 1.5f;
        private const float ShrinkTime = 4f;

        // Seconds a rock takes to settle into its asteroid's motion after it changes.
        private const float FollowTime = 4f;

        // Rocks wandering wider than their asteroid ease back to the trail's line, at most this fast.
        private const float GatherRate = 0.02f;
        private const float MaxGatherSpeed = 0.3f;

        // A rock leaving physics this much faster or slower than its trail was knocked loose, and flies free.
        private const float LooseSpeed = 0.75f;

        // Rocks owed by one trail at most, so a trail held back by a full budget never releases a burst later.
        private const float MaxCharge = 4f;

        private const float AssignInterval = 0.1f;

        // New rocks drift clear of their asteroid's lumps before they turn solid, so none starts inside the rock.
        private const float MinSolidAge = 4f;

        // Solid rocks stay solid a little beyond the range, so rocks at its edge do not flicker in and out.
        private const float ReleaseSlack = 1.2f;

        // A trail of a 40 m asteroid has the type's rock count and sizes; larger ones have more and bigger rocks.
        private const float ReferenceDiameter = 40f;

        // Below this drift, in metres per second, a trail keeps its last direction rather than following noise.
        private const float StillSpeed = 0.05f;

        // How quickly a trail swings round to follow a change in drift, per second.
        private const float TurnRate = 0.5f;

        // Rocks leave from the back of the asteroid: within about 55 degrees of straight behind it.
        private const float ConeWeight = 1.2f;

        // Rocks are born this share of the asteroid's diameter from its centre: just outside its mean surface.
        private const float SpawnRadiusShare = 0.6f;

        // Each rock's own drift off the trail's line, so the trail has width.
        private const float SpreadSpeed = 0.05f;
        private const float MinRockSpin = 0.1f;
        private const float MaxRockSpin = 0.8f;

        // A lumpy rock fills about this share of the ellipsoid it fits in; its ball collider sits just inside the lumps.
        private const float FilledShare = 0.8f;
        private const float BallShare = 0.45f;
        private const float MinMass = 0.0005f;

        private static readonly Color DefaultChipColor = new Color(0.55f, 0.46f, 0.38f);

        // Read by the physics contact hook on worker threads while the simulation runs, and only written on the main
        // thread between steps, so it needs no lock.
        private static readonly HashSet<int> rockColliders = new HashSet<int>();
        private static float shipKick = 1f;

        private readonly DestructionSystem system;
        private readonly Transform proxyRoot;
        private readonly Mesh mesh;
        private readonly PhysicsMaterial surface;
        private readonly List<TrailRockProxy> proxies = new List<TrailRockProxy>();
        private readonly Stack<TrailRockProxy> freeProxies = new Stack<TrailRockProxy>();
        private readonly List<TrailRockProxy> shattered = new List<TrailRockProxy>();
        private readonly List<AsteroidDefinition> kinds = new List<AsteroidDefinition>();
        private readonly List<Candidate> candidates = new List<Candidate>();
        private readonly List<VoxelBody> ownerBodies = new List<VoxelBody>();
        private readonly List<int> ownerRockCounts = new List<int>();
        private readonly Stack<int> freeOwnerSlots = new Stack<int>();

        private NativeArray<TrailRock> rocks;
        private NativeArray<Matrix4x4> matrices;
        private NativeArray<TrailOwner> owners;
        private int count;
        private JobHandle handle;
        private bool scheduled;
        private Material material;
        private Material instancedCopy;
        private Bounds bounds;
        private float nextAssign;
        private Random random;

        public TrailRocks(DestructionSystem system, Transform parent, int capacity)
        {
            this.system = system;
            capacity = math.max(capacity, 0);
            rocks = new NativeArray<TrailRock>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            matrices = new NativeArray<Matrix4x4>(capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            owners = new NativeArray<TrailOwner>(InitialOwnerSlots, Allocator.Persistent);
            random = new Random(0x6E624EB7u);
            mesh = CreateRockMesh();

            // Rock on hull: no grip to twist the ship round, and little bounce.
            surface = new PhysicsMaterial("Trail Rock")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0.2f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            proxyRoot = new GameObject("Trail Rocks").transform;
            proxyRoot.SetParent(parent, false);

            Physics.ContactModifyEvent += SoftenShipHits;
            Physics.ContactModifyEventCCD += SoftenShipHits;
        }

        /// <summary>Trail rocks in the scene.</summary>
        public int Count => count;

        /// <summary>Trail rocks solid right now, near the camera.</summary>
        public int SolidCount { get; private set; }

        public int Capacity => rocks.IsCreated ? rocks.Length : 0;

        /// <summary>
        /// Early in the frame: takes the solid rocks' poses from physics, drops dead rocks, grows the trails and starts
        /// the job that moves every rock. Nothing may touch the rocks again until <see cref="EndFrame"/>.
        /// </summary>
        public void BeginFrame(float deltaTime)
        {
            if (Capacity == 0)
                return;

            // Normally finished in late update; a frame that skipped it must not leave the job holding the rocks.
            CompleteJob();
            SyncProxies();
            RemoveDead();

            Camera camera = Camera.main;
            if (camera != null && deltaTime > 0f)
                Emit(camera.transform.position, deltaTime);

            RefreshOwners();
            if (count == 0)
                return;

            handle = new TrailRockJob
            {
                Rocks = rocks,
                Owners = owners,
                Matrices = matrices,
                DeltaTime = deltaTime,
                FollowTime = FollowTime,
                GatherRate = GatherRate,
                MaxGatherSpeed = MaxGatherSpeed,
                GrowTime = GrowTime,
                ShrinkTime = ShrinkTime
            }.Schedule(count, JobBatch);

            scheduled = true;
            JobHandle.ScheduleBatchedJobs();
        }

        /// <summary>Late in the frame: finishes the job, draws every rock, and hands the nearest rocks to physics.</summary>
        public void EndFrame()
        {
            if (Capacity == 0)
                return;

            CompleteJob();
            FinishShattered();
            Draw();
            shipKick = system.ShipKick;

            Camera camera = Camera.main;
            if (camera != null && Time.unscaledTime >= nextAssign)
            {
                nextAssign = Time.unscaledTime + AssignInterval;
                UpdateProxies(camera.transform.position);
            }
        }

        /// <summary>Removes every trail rock. Trails grow back from their asteroids.</summary>
        public void Clear()
        {
            CompleteJob();
            foreach (TrailRockProxy proxy in proxies)
                Release(proxy);

            FinishShattered();
            count = 0;
            for (int slot = 0; slot < ownerRockCounts.Count; ++slot)
                ownerRockCounts[slot] = 0;
        }

        /// <summary>Breaks a solid rock into chips. Called by its stand-in when a weapon hits it.</summary>
        internal void Shatter(TrailRockProxy proxy, in DamageEvent damage)
        {
            if (!proxy.IsActive)
                return;

            Vector3 position = proxy.transform.position;
            float radius = proxy.Ball.radius;
            Vector3 normal = damage.Direction.sqrMagnitude > 0.5f ? -damage.Direction : (position - damage.Point).normalized;
            AsteroidDefinition kind = proxy.Kind < kinds.Count ? kinds[proxy.Kind] : null;
            Color color = kind != null ? kind.ChipColor(0) : DefaultChipColor;

            // Enough chips that the rock visibly bursts, however small it was.
            float volume = math.max(4f / 3f * math.PI * radius * radius * radius, 2f);
            system.Debris.Burst(position, normal, color, volume, radius * 2f);

            // The rock itself is retired once the job is done with it; the stand-in stops colliding now.
            proxy.Deactivate();
            SolidCount--;
            shattered.Add(proxy);
        }

        public void Dispose()
        {
            CompleteJob();
            Physics.ContactModifyEvent -= SoftenShipHits;
            Physics.ContactModifyEventCCD -= SoftenShipHits;
            rockColliders.Clear();

            if (rocks.IsCreated)
                rocks.Dispose();
            if (matrices.IsCreated)
                matrices.Dispose();
            if (owners.IsCreated)
                owners.Dispose();

            if (mesh != null)
                UnityEngine.Object.Destroy(mesh);
            if (instancedCopy != null)
                UnityEngine.Object.Destroy(instancedCopy);
            if (surface != null)
                UnityEngine.Object.Destroy(surface);
        }

        private void CompleteJob()
        {
            if (!scheduled)
                return;

            handle.Complete();
            scheduled = false;
        }

        // Runs inside the physics step, on worker threads, for every contact a trail rock's collider is part of. In a hit
        // between a trail rock and anything else that moves, the other body acts heavier and harder to turn by the ship
        // kick, so a ship knocks the rock flying but barely feels it, however fast it was going.
        private static void SoftenShipHits(PhysicsScene scene, NativeArray<ModifiableContactPair> pairs)
        {
            float scale = shipKick;
            for (int i = 0; i < pairs.Length; ++i)
            {
                ModifiableContactPair pair = pairs[i];
                bool rockFirst = rockColliders.Contains(pair.colliderInstanceID);
                bool rockSecond = rockColliders.Contains(pair.otherColliderInstanceID);
                if (rockFirst == rockSecond)
                    continue;

                ModifiableMassProperties mass = pair.massProperties;
                if (rockFirst)
                {
                    mass.otherInverseMassScale = scale;
                    mass.otherInverseInertiaScale = scale;
                }
                else
                {
                    mass.inverseMassScale = scale;
                    mass.inverseInertiaScale = scale;
                }

                pair.massProperties = mass;
                pairs[i] = pair;
            }
        }

        // Solid rocks are moved by physics; the job only draws them, so their state comes from their stand-ins.
        private void SyncProxies()
        {
            foreach (TrailRockProxy proxy in proxies)
            {
                if (!proxy.IsActive)
                    continue;

                TrailRock rock = rocks[proxy.Rock];
                Transform pose = proxy.transform;
                rock.Position = pose.position;
                rock.Rotation = pose.rotation;
                rock.Velocity = proxy.Body.linearVelocity;
                rock.Spin = proxy.Body.angularVelocity;
                rocks[proxy.Rock] = rock;
            }
        }

        // Drops rocks that reached the end of their life, filling each gap with the last rock, and measures the bounds
        // the batch is culled with.
        private void RemoveDead()
        {
            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            float largest = 0f;
            int i = 0;
            while (i < count)
            {
                TrailRock rock = rocks[i];
                if (rock.IsDead)
                {
                    if (rock.Proxy >= 0)
                        Release(proxies[rock.Proxy]);
                    if (rock.Owner >= 0 && rock.Owner < ownerRockCounts.Count)
                        ownerRockCounts[rock.Owner]--;

                    int last = --count;
                    if (i != last)
                    {
                        TrailRock moved = rocks[last];
                        rocks[i] = moved;
                        if (moved.Proxy >= 0)
                            proxies[moved.Proxy].Rock = i;
                    }

                    continue;
                }

                min = math.min(min, rock.Position);
                max = math.max(max, rock.Position);
                largest = math.max(largest, math.cmax(rock.Scale));
                ++i;
            }

            // Room for one frame of motion and the rocks' own size.
            bounds = count > 0 ? new Bounds((min + max) * 0.5f, max - min + new float3(largest + 20f)) : default;
        }

        private void Emit(Vector3 focus, float deltaTime)
        {
            IReadOnlyList<VoxelBody> bodies = system.Bodies;
            float rangeSq = system.TrailRange * system.TrailRange;
            for (int b = 0; b < bodies.Count; ++b)
            {
                VoxelBody body = bodies[b];
                if (body == null || !body.IsAllocated || body.IsReleased || !body.HasBeenMeshed)
                    continue;

                AsteroidDefinition definition = body.Definition;
                if (definition == null || definition.TrailRocks <= 0)
                    continue;

                float diameter = body.EquivalentDiameter;
                Vector3 centre = body.CentreOfMass;
                if (diameter < definition.MinTrailDiameter || (centre - focus).sqrMagnitude > rangeSq)
                {
                    body.TrailCharge = 0f;
                    body.TrailInRange = false;
                    continue;
                }

                EnsureMaterial(definition);
                bool isNew = body.TrailBack == Vector3.zero;
                bool entering = !body.TrailInRange;
                body.TrailInRange = true;
                SteerTrail(body, deltaTime);
                int slot = OwnerSlot(body);

                float scale = diameter / ReferenceDiameter;
                float rocksPerTrail = definition.TrailRocks * math.clamp(scale, 0.5f, 3f);
                float lifetime = definition.TrailLength * diameter / math.max(definition.TrailDriftSpeed, 0.01f);

                // A trail coming into range, far beyond sight, is topped up to full, as if the asteroid had been shedding
                // all along: one seen for the first time, or one that thinned out while it was out of range. A piece
                // that just broke away starts with none.
                if (entering && !(isNew && body.IsPiece))
                {
                    int missing = (int)rocksPerTrail - ownerRockCounts[slot];
                    for (int n = 0; n < missing && count < Capacity; ++n)
                        Spawn(body, slot, definition, centre, diameter, scale, lifetime, true);
                }

                body.TrailCharge = math.min(body.TrailCharge + rocksPerTrail / lifetime * deltaTime, MaxCharge);
                while (body.TrailCharge >= 1f && count < Capacity)
                {
                    Spawn(body, slot, definition, centre, diameter, scale, lifetime, false);
                    body.TrailCharge -= 1f;
                }
            }
        }

        // The trail streams out against the asteroid's drift and swings round slowly when the drift changes.
        private static void SteerTrail(VoxelBody body, float deltaTime)
        {
            Vector3 drift = body.LinearVelocity;
            float speed = drift.magnitude;
            if (body.TrailBack == Vector3.zero)
            {
                body.TrailBack = speed > StillSpeed ? -drift / speed : StableDirection(body);
                return;
            }

            if (speed > StillSpeed)
                body.TrailBack = Vector3.Slerp(body.TrailBack, -drift / speed, 1f - Mathf.Exp(-TurnRate * deltaTime)).normalized;
        }

        // A still asteroid's trail points a fixed way of its own, the same every time it is seen.
        private static Vector3 StableDirection(VoxelBody body)
        {
            var hash = new Random(math.hash(new int2(body.GetInstanceID(), 0x2C1B3C6D)) | 1u);
            return hash.NextFloat3Direction();
        }

        // The body's slot in the owner table the job follows, given one the first time it trails.
        private int OwnerSlot(VoxelBody body)
        {
            int slot = body.TrailSlot;
            if (slot >= 0 && slot < ownerBodies.Count && ownerBodies[slot] == body)
                return slot;

            if (freeOwnerSlots.Count > 0)
            {
                slot = freeOwnerSlots.Pop();
                ownerBodies[slot] = body;
                ownerRockCounts[slot] = 0;
            }
            else
            {
                slot = ownerBodies.Count;
                ownerBodies.Add(body);
                ownerRockCounts.Add(0);
                if (slot >= owners.Length)
                {
                    // Only ever called before the job is scheduled, so the table can grow here.
                    var grown = new NativeArray<TrailOwner>(owners.Length * 2, Allocator.Persistent);
                    NativeArray<TrailOwner>.Copy(owners, grown, owners.Length);
                    owners.Dispose();
                    owners = grown;
                }
            }

            body.TrailSlot = slot;
            return slot;
        }

        // Writes where every trailing asteroid is and how it moves. A slot whose asteroid is gone is freed, and its rocks
        // fly on by themselves.
        private void RefreshOwners()
        {
            for (int slot = 0; slot < ownerBodies.Count; ++slot)
            {
                VoxelBody body = ownerBodies[slot];
                if (ReferenceEquals(body, null))
                    continue;

                if (body == null || !body.IsAllocated || body.IsReleased || body.TrailSlot != slot)
                {
                    FreeOwnerSlot(slot);
                    continue;
                }

                owners[slot] = new TrailOwner
                {
                    Centre = body.CentreOfMass,
                    Velocity = body.LinearVelocity,
                    Back = body.TrailBack,
                    Radius = body.EquivalentDiameter * 0.5f,
                    Active = true
                };
            }
        }

        private void FreeOwnerSlot(int slot)
        {
            owners[slot] = default;
            ownerBodies[slot] = null;
            ownerRockCounts[slot] = 0;
            freeOwnerSlots.Push(slot);
            for (int i = 0; i < count; ++i)
            {
                TrailRock rock = rocks[i];
                if (rock.Owner != slot)
                    continue;

                rock.Owner = -1;
                rocks[i] = rock;
            }
        }

        private void Spawn(VoxelBody body, int slot, AsteroidDefinition definition, Vector3 centre, float diameter,
            float scale, float lifetime, bool anyAge)
        {
            float3 back = body.TrailBack;
            float3 outward = math.normalize(back * ConeWeight + random.NextFloat3Direction());
            float backSpeed = definition.TrailDriftSpeed * random.NextFloat(0.7f, 1.3f);
            float3 spread = random.NextFloat3Direction() * (SpreadSpeed * random.NextFloat());

            float life = lifetime * random.NextFloat(0.8f, 1.2f);
            float age = anyAge ? random.NextFloat() * life : 0f;
            float size = random.NextFloat(definition.MinTrailRockSize, definition.MaxTrailRockSize) * math.sqrt(math.clamp(scale, 0.25f, 4f));

            // Born at the asteroid's back, moving with it; a rock given an age has slid that far down the trail since.
            ownerRockCounts[slot]++;
            rocks[count++] = new TrailRock
            {
                Position = (float3)centre + outward * (diameter * SpawnRadiusShare) + (back * backSpeed + spread) * age,
                Velocity = (float3)body.LinearVelocity + back * backSpeed + spread,
                Rotation = random.NextQuaternionRotation(),
                Spin = random.NextFloat3Direction() * random.NextFloat(MinRockSpin, MaxRockSpin),
                Scale = size * random.NextFloat3(0.7f, 1.3f),
                Age = age,
                Lifetime = life,
                Kind = KindIndex(definition),
                Owner = slot,
                BackSpeed = backSpeed,
                Spread = spread,
                Proxy = -1
            };
        }

        private int KindIndex(AsteroidDefinition definition)
        {
            int index = kinds.IndexOf(definition);
            if (index >= 0)
                return index;

            kinds.Add(definition);
            return kinds.Count - 1;
        }

        private void EnsureMaterial(AsteroidDefinition definition)
        {
            if (material != null)
                return;

            Material source = system.TrailMaterial != null ? system.TrailMaterial : definition.SurfaceMaterial;
            if (source == null)
                return;

            // Instanced drawing needs a material that allows it; a copy leaves the asteroid's own material untouched.
            if (source.enableInstancing)
            {
                material = source;
                return;
            }

            instancedCopy = new Material(source) { name = source.name + " (Trail Rocks)", enableInstancing = true };
            material = instancedCopy;
        }

        private void Draw()
        {
            if (count == 0 || material == null)
                return;

            var parameters = new RenderParams(material)
            {
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                lightProbeUsage = LightProbeUsage.Off
            };

            for (int start = 0; start < count; start += BatchSize)
                Graphics.RenderMeshInstanced(parameters, mesh, 0, matrices, math.min(BatchSize, count - start), start);
        }

        // Retires rocks shattered since the job started, and frees their stand-ins.
        private void FinishShattered()
        {
            foreach (TrailRockProxy proxy in shattered)
            {
                int index = proxy.Rock;
                if (index >= 0 && index < count)
                {
                    TrailRock rock = rocks[index];
                    if (rock.Proxy == proxy.Index)
                    {
                        rock.Age = rock.Lifetime;
                        rock.Proxy = -1;
                        rocks[index] = rock;
                    }
                }

                proxy.Rock = -1;
                freeProxies.Push(proxy);
            }

            shattered.Clear();
        }

        // Rocks near the camera turn solid, nearest first, up to the budget; rocks that left the range or are shrinking
        // away go back to the job.
        private void UpdateProxies(Vector3 focus)
        {
            float range = system.TrailPhysicsRange;
            int budget = system.MaxSolidTrailRocks;
            float keepSq = range * ReleaseSlack * range * ReleaseSlack;
            foreach (TrailRockProxy proxy in proxies)
            {
                if (!proxy.IsActive)
                    continue;

                TrailRock rock = rocks[proxy.Rock];
                if (budget <= 0 || math.lengthsq(rock.Position - (float3)focus) > keepSq || rock.Lifetime - rock.Age < ShrinkTime)
                    Release(proxy);
            }

            if (budget <= 0 || range <= 0f)
                return;

            float rangeSq = range * range;
            candidates.Clear();
            for (int i = 0; i < count; ++i)
            {
                TrailRock rock = rocks[i];
                if (rock.Proxy >= 0 || rock.Age < MinSolidAge || rock.Lifetime - rock.Age < ShrinkTime)
                    continue;

                float distanceSq = math.lengthsq(rock.Position - (float3)focus);
                if (distanceSq <= rangeSq)
                    candidates.Add(new Candidate(i, distanceSq));
            }

            if (candidates.Count > budget - SolidCount)
                candidates.Sort(CandidateOrder.Instance);

            for (int c = 0; c < candidates.Count && SolidCount < budget; ++c)
                Claim(candidates[c].Rock, budget);
        }

        private void Claim(int index, int budget)
        {
            TrailRock rock = rocks[index];
            float radius = math.csum(rock.Scale) / 3f * BallShare;

            // Never turn solid inside something: physics would shove the two apart hard. It tries again next time.
            if (Physics.CheckSphere(rock.Position, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;

            TrailRockProxy proxy;
            if (freeProxies.Count > 0)
                proxy = freeProxies.Pop();
            else if (proxies.Count < budget)
            {
                proxy = TrailRockProxy.Create(this, proxies.Count, proxyRoot, surface);
                proxies.Add(proxy);
                rockColliders.Add(proxy.Ball.GetInstanceID());
            }
            else
                return;

            AsteroidDefinition kind = rock.Kind < kinds.Count ? kinds[rock.Kind] : null;
            float density = kind != null ? kind.Density : 2.5f;
            float tonnes = density * math.PI / 6f * rock.Scale.x * rock.Scale.y * rock.Scale.z * FilledShare;

            proxy.Activate(index, rock.Kind, rock.Position, rock.Rotation, rock.Velocity, rock.Spin, radius,
                math.max(tonnes * system.SolidRockMassPerTonne, MinMass));
            rock.Proxy = proxy.Index;
            rocks[index] = rock;
            SolidCount++;
        }

        // The rock goes back to the job from where physics left it; its state was taken at the start of the frame. If a
        // ship or another rock knocked it off its trail, it stays off and flies on.
        private void Release(TrailRockProxy proxy)
        {
            if (!proxy.IsActive)
                return;

            proxy.Deactivate();
            SolidCount--;
            int index = proxy.Rock;
            if (index >= 0 && index < count)
            {
                TrailRock rock = rocks[index];
                if (rock.Proxy == proxy.Index)
                {
                    rock.Proxy = -1;
                    if (rock.Owner >= 0 && rock.Owner < owners.Length && owners[rock.Owner].Active)
                    {
                        float3 target = TrailRockJob.Target(rock, owners[rock.Owner], GatherRate, MaxGatherSpeed);
                        if (math.lengthsq(rock.Velocity - target) > LooseSpeed * LooseSpeed)
                        {
                            ownerRockCounts[rock.Owner]--;
                            rock.Owner = -1;
                        }
                    }

                    rocks[index] = rock;
                }
            }

            proxy.Rock = -1;
            freeProxies.Push(proxy);
        }

        // A lumpy ball: an icosphere split once (80 triangles) with each corner pushed in or out. Shared corners give the
        // smooth normals the comic outline follows. Vertex colour carries no ore.
        private static Mesh CreateRockMesh()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };

            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };

            foreach (Vector3 corner in corners)
                vertices.Add(corner.normalized);

            var midpoints = new Dictionary<long, int>();
            for (int f = 0; f < faces.Length; f += 3)
            {
                int a = faces[f], b = faces[f + 1], c = faces[f + 2];
                int ab = Midpoint(a, b, vertices, midpoints);
                int bc = Midpoint(b, c, vertices, midpoints);
                int ca = Midpoint(c, a, vertices, midpoints);
                AddOutward(triangles, vertices, a, ab, ca);
                AddOutward(triangles, vertices, b, bc, ab);
                AddOutward(triangles, vertices, c, ca, bc);
                AddOutward(triangles, vertices, ab, bc, ca);
            }

            var lumps = new Random(0x1B873593u);
            var colors = new Color[vertices.Count];
            for (int i = 0; i < vertices.Count; ++i)
            {
                vertices[i] *= 0.5f * lumps.NextFloat(0.8f, 1.15f);
                colors[i] = new Color(1f, 1f, 1f, 0f);
            }

            var rock = new Mesh { name = "Trail Rock" };
            rock.SetVertices(vertices);
            rock.SetColors(colors);
            rock.SetTriangles(triangles, 0);
            rock.RecalculateNormals();
            rock.RecalculateBounds();
            rock.UploadMeshData(true);
            return rock;
        }

        private static int Midpoint(int a, int b, List<Vector3> vertices, Dictionary<long, int> midpoints)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (midpoints.TryGetValue(key, out int index))
                return index;

            index = vertices.Count;
            vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
            midpoints.Add(key, index);
            return index;
        }

        // Winds each triangle to face away from the centre, as Unity draws front faces.
        private static void AddOutward(List<int> triangles, List<Vector3> vertices, int a, int b, int c)
        {
            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            bool outward = Vector3.Dot(normal, vertices[a] + vertices[b] + vertices[c]) > 0f;
            triangles.Add(a);
            triangles.Add(outward ? b : c);
            triangles.Add(outward ? c : b);
        }

        private readonly struct Candidate
        {
            public readonly int Rock;
            public readonly float DistanceSq;

            public Candidate(int rock, float distanceSq)
            {
                Rock = rock;
                DistanceSq = distanceSq;
            }
        }

        private sealed class CandidateOrder : IComparer<Candidate>
        {
            public static readonly CandidateOrder Instance = new CandidateOrder();

            public int Compare(Candidate a, Candidate b)
            {
                return a.DistanceSq.CompareTo(b.DistanceSq);
            }
        }
    }
}
