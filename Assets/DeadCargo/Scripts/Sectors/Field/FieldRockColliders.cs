using System;
using System.Collections.Generic;
using DeadCargo.Destruction;
using DeadCargo.Destruction.Voxels;
using DeadCargo.Flow;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace DeadCargo.Sectors.Field
{
    /// <summary>
    /// Makes the field rocks near the player solid. Each solid rock borrows a pooled <see cref="FieldRockProxy"/>: a
    /// kinematic body whose mesh collider is the rock's own shape, at a level of detail picked by the rock's size, so
    /// a boulder costs a few dozen triangles and a giant is solid to within a few metres of what is drawn. The shapes
    /// are cooked once, on worker threads, when the field is built; a rock turning solid cooks nothing.
    /// <see cref="FieldSolidJob"/> picks which rocks are solid; this applies its answer.
    /// </summary>
    internal sealed class FieldRockColliders : IDisposable
    {
        // The coarsest level, 20 triangles, is too rough to fly against.
        private const int FirstLevel = 1;
        private const int LevelCount = FieldRockShape.Levels - FirstLevel;

        // A collider's triangles stay under about this long, in metres, until the finest level runs out. The first
        // level's edges are about half the rock's radius, and each level halves them.
        private const float TargetEdge = 3f;
        private const float FirstLevelEdgePerRadius = 0.5f;

        // Rocks turning solid in one pass, so a burst of them never costs a frame. The nearest go first.
        private const int MaxActivationsPerPass = 32;

        private readonly AsteroidField owner;
        private readonly Transform root;
        private readonly PhysicsMaterial surface;
        private readonly Dictionary<int, FieldRockProxy> solid = new Dictionary<int, FieldRockProxy>();
        private readonly List<FieldRockProxy> proxies = new List<FieldRockProxy>();
        private readonly Stack<FieldRockProxy> idle = new Stack<FieldRockProxy>();
        private readonly List<FieldRockProxy> releasing = new List<FieldRockProxy>();

        private NativeArray<EntityId> bakeIds;
        private JobHandle bake;
        private bool isBaking;
        private bool isBaked;
        private int pass;

        public FieldRockColliders(AsteroidField owner)
        {
            this.owner = owner;
            root = new GameObject("Field Rock Colliders").transform;
            root.SetParent(owner.transform, false);

            // Rock on hull: no grip to twist the ship round, and little bounce.
            surface = new PhysicsMaterial("Field Rock")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0.2f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
        }

        /// <summary>Rocks solid now.</summary>
        public int Count => solid.Count;

        /// <summary>True once the shapes are cooked and rocks can turn solid.</summary>
        public bool IsReady
        {
            get
            {
                if (isBaking && bake.IsCompleted)
                {
                    bake.Complete();
                    isBaking = false;
                    isBaked = true;
                    LoadReport.EndSpan("Rock shapes");
                }

                return isBaked;
            }
        }

        /// <summary>The level of detail a rock's collider uses: finer for larger rocks.</summary>
        public static int LevelFor(float radius)
        {
            int level = FirstLevel;
            float edge = radius * FirstLevelEdgePerRadius;
            while (level < FieldRockShape.Levels - 1 && edge > TargetEdge)
            {
                ++level;
                edge *= 0.5f;
            }

            return level;
        }

        /// <summary>
        /// Starts cooking every shape's collider levels on worker threads, and makes sure the pool holds
        /// <paramref name="poolSize"/> stand-ins. Call after the meshes are built, with no rock solid.
        /// </summary>
        public void Prepare(FieldRockMeshes meshes, int poolSize)
        {
            using LoadScope measure = LoadReport.Measure("Rock shapes");
            int count = meshes.Shapes * LevelCount;
            bakeIds = new NativeArray<EntityId>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int shape = 0; shape < meshes.Shapes; ++shape)
            {
                for (int level = FirstLevel; level < FieldRockShape.Levels; ++level)
                    bakeIds[shape * LevelCount + level - FirstLevel] = meshes.Get(shape, level).GetEntityId();
            }

            bake = new ColliderBakeJob { MeshIds = bakeIds }.Schedule(count, 1);
            isBaking = true;
            isBaked = false;
            LoadReport.StartSpan("Rock shapes");
            JobHandle.ScheduleBatchedJobs();

            while (proxies.Count < poolSize)
                AddProxy();
        }

        /// <summary>
        /// Applies a selection pass: the nearest <paramref name="max"/> candidates are solid, and every other rock
        /// stops being so.
        /// </summary>
        /// <param name="candidates">Nearest first, from <see cref="FieldSolidJob"/>.</param>
        public void Apply(NativeArray<SolidCandidate> candidates, int found, NativeArray<FieldRock> rocks, in FieldSettings settings,
            FieldRockMeshes meshes, SectorSpace space, int max, float time)
        {
            ++pass;
            int kept = 0;
            int activated = 0;
            for (int i = 0; i < found && kept < max; ++i)
            {
                SolidCandidate candidate = candidates[i];
                if (solid.TryGetValue(candidate.Rock, out FieldRockProxy proxy))
                {
                    proxy.Stamp = pass;
                    ++kept;
                    continue;
                }

                if (!candidate.InRange || activated >= MaxActivationsPerPass)
                    continue;

                FieldRock rock = rocks[candidate.Rock];
                Mesh mesh = meshes.Get(FieldRockShape.ShapeOf(rock.Seed, meshes.Shapes), LevelFor(rock.Radius));
                proxy = idle.Count > 0 ? idle.Pop() : AddProxy(false);
                proxy.Activate(candidate.Rock, rock, settings, mesh, space.SectorToWorld(rock.Position), time);
                proxy.Stamp = pass;
                solid.Add(candidate.Rock, proxy);
                ++kept;
                ++activated;
            }

            foreach (KeyValuePair<int, FieldRockProxy> entry in solid)
            {
                if (entry.Value.Stamp != pass)
                    releasing.Add(entry.Value);
            }

            ReleaseMarked();
        }

        /// <summary>Turns every solid rock to where it has tumbled to. Call every physics step.</summary>
        public void Step(float time)
        {
            foreach (KeyValuePair<int, FieldRockProxy> entry in solid)
                entry.Value.Step(time);
        }

        /// <summary>Releases the solid rocks of one cell slot, whose cell is being unloaded.</summary>
        public void ReleaseSlot(int slot, int capacity)
        {
            if (solid.Count == 0)
                return;

            foreach (KeyValuePair<int, FieldRockProxy> entry in solid)
            {
                if (entry.Key / capacity == slot)
                    releasing.Add(entry.Value);
            }

            ReleaseMarked();
        }

        /// <summary>No rock is solid after this.</summary>
        public void ReleaseAll()
        {
            foreach (KeyValuePair<int, FieldRockProxy> entry in solid)
                releasing.Add(entry.Value);

            ReleaseMarked();
        }

        /// <summary>
        /// Lets go of the meshes, which are about to be rebuilt or destroyed: no rock is solid, no cooking is still
        /// running, and no stand-in holds a mesh. <see cref="Prepare"/> makes it ready again.
        /// </summary>
        public void Reset()
        {
            ReleaseAll();
            bake.Complete();
            isBaking = false;
            isBaked = false;
            if (bakeIds.IsCreated)
                bakeIds.Dispose();

            // When the scene is closing, the stand-ins may already be gone.
            for (int i = 0; i < proxies.Count; ++i)
            {
                if (proxies[i] != null)
                    proxies[i].ClearShape();
            }
        }

        public void Dispose()
        {
            Reset();
            if (root != null)
                UnityEngine.Object.Destroy(root.gameObject);

            UnityEngine.Object.Destroy(surface);
            proxies.Clear();
            idle.Clear();
        }

        internal void ReportDamage(FieldRockProxy proxy, in DamageEvent damage)
        {
            owner.ReportRockDamaged(proxy, damage);
        }

        private FieldRockProxy AddProxy(bool toPool = true)
        {
            FieldRockProxy proxy = FieldRockProxy.Create(this, root, owner.gameObject.layer, surface);
            proxies.Add(proxy);
            if (toPool)
                idle.Push(proxy);

            return proxy;
        }

        private void ReleaseMarked()
        {
            for (int i = 0; i < releasing.Count; ++i)
            {
                FieldRockProxy proxy = releasing[i];
                solid.Remove(proxy.RockIndex);
                if (proxy == null)
                    continue;

                proxy.Deactivate();
                idle.Push(proxy);
            }

            releasing.Clear();
        }
    }
}
