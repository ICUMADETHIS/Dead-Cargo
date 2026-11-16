using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCargo.Destruction
{
    /// <summary>
    /// Rock chips for every cut in the scene, from one particle system: no objects are spawned, and every chip in the
    /// scene draws in one batch. Each cut is one emission; the particle system scatters, sizes and spins the chips
    /// itself. Chips fly out of the cut, tumble, shrink and vanish.
    /// <para>
    /// Cuts queue their chips, and the queue is thrown at the start of the next frame: the crater's new mesh shows that
    /// frame too, so chips and crater appear together, and the particle system is never touched while its own update
    /// job runs. A budget per second, banked through quiet moments, shares chips between the cuts of a busy frame. It
    /// is held below what the pool can keep in the air, so the pool never fills and the newest cut always throws chips.
    /// </para>
    /// </summary>
    internal sealed class DebrisParticles
    {
        private const float MinSpeed = 2f;
        private const float MaxSpeed = 9f;
        private const float SpreadAngle = 55f;
        private const float MaxSpin = 7f;
        private const float MinLifetime = 1.2f;
        private const float MaxLifetime = 2.6f;

        // Budget a quiet moment banks, in seconds of the rate, so one big blast still throws its full burst.
        private const float BankSeconds = 0.1f;

        // Cuts queued in one frame. Past this, a frame's chips are already spread too thin to show more cuts.
        private const int QueueCapacity = 128;

        private struct PendingBurst
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Color Color;
            public float Radius;
            public int Count;
        }

        private readonly ParticleSystem particles;
        private readonly PendingBurst[] queue = new PendingBurst[QueueCapacity];
        private readonly float chipsPerCubicMetre;
        private readonly int maxChipsPerBurst;
        private readonly float bankLimit;
        private readonly float chipSize;
        private int queued;
        private int queuedChips;
        private float bank;

        /// <param name="maxChips">Most chips in the air at once: the size of the pool.</param>
        /// <param name="maxChipsPerSecond">Most chips thrown per second. Lowered if the pool could not hold them.</param>
        public DebrisParticles(Transform parent, Material material, int maxChips, float chipsPerCubicMetre, int maxChipsPerBurst,
            int maxChipsPerSecond, float chipSize)
        {
            this.chipsPerCubicMetre = chipsPerCubicMetre;
            this.maxChipsPerBurst = maxChipsPerBurst;
            this.chipSize = chipSize;

            // Chips alive at once never pass the bank plus a full lifetime of the rate, so that must fit the pool.
            bankLimit = Mathf.Min(Mathf.Max(maxChipsPerBurst * 2f, maxChipsPerSecond * BankSeconds), maxChips * 0.25f);
            ChipsPerSecond = Mathf.Min(maxChipsPerSecond, (maxChips - bankLimit) / MaxLifetime);
            bank = bankLimit;

            var holder = new GameObject("Rock Chips");
            holder.transform.SetParent(parent, false);
            particles = holder.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxChips;
            main.startLifetime = new ParticleSystem.MinMaxCurve(MinLifetime, MaxLifetime);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            // Chips leave in a cone around the cut's facing.
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = SpreadAngle;
            shape.radiusThickness = 1f;

            ParticleSystem.RotationOverLifetimeModule spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-MaxSpin, MaxSpin);
            spin.y = new ParticleSystem.MinMaxCurve(-MaxSpin, MaxSpin);
            spin.z = new ParticleSystem.MinMaxCurve(-MaxSpin, MaxSpin);

            // Chips hold their size, then shrink away: no fading, which would not suit the comic look.
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.65f, 0.9f), new Keyframe(1f, 0f)));

            var renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = CreateChipMesh();
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            particles.Play();
        }

        public int Count => particles != null ? particles.particleCount : 0;

        /// <summary>Chips thrown per second at most, after fitting the budget to the pool.</summary>
        public float ChipsPerSecond { get; }

        /// <summary>Queues chips out of a cut. They are thrown by the next <see cref="Flush"/>.</summary>
        /// <param name="volume">Rock removed, in cubic metres; sets how many chips fly.</param>
        /// <param name="radius">Size of the cut, in metres; sets how far apart and how big the chips are.</param>
        public void Burst(Vector3 position, Vector3 normal, Color color, float volume, float radius)
        {
            if (particles == null || volume <= 0f || queued >= QueueCapacity)
                return;

            int count = Mathf.Clamp(Mathf.RoundToInt(volume * chipsPerCubicMetre), 1, maxChipsPerBurst);
            queue[queued++] = new PendingBurst { Position = position, Normal = normal, Color = color, Radius = radius, Count = count };
            queuedChips += count;
        }

        /// <summary>
        /// Throws the queued chips. Called once a frame, early. When the budget is short, every cut gets its share, and
        /// at least one chip while any are left.
        /// </summary>
        public void Flush(float deltaTime)
        {
            bank = Mathf.Min(bank + ChipsPerSecond * Mathf.Max(deltaTime, 0f), bankLimit);
            if (queued == 0 || particles == null)
                return;

            float share = queuedChips > bank ? bank / queuedChips : 1f;
            for (int i = 0; i < queued && bank >= 1f; ++i)
            {
                int count = Mathf.Min(Mathf.Max(1, (int)(queue[i].Count * share)), (int)bank);
                bank -= count;
                Emit(queue[i], count);
            }

            queued = 0;
            queuedChips = 0;
        }

        public void Clear()
        {
            queued = 0;
            queuedChips = 0;
            if (particles != null)
                particles.Clear();
        }

        private void Emit(in PendingBurst burst, int count)
        {
            float scale = Mathf.Clamp(burst.Radius, 0.4f, 3f);
            Vector3 normal = burst.Normal.sqrMagnitude < 0.5f ? Vector3.up : burst.Normal;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.rotation = Quaternion.LookRotation(normal).eulerAngles;
            shape.radius = burst.Radius * 0.5f;

            ParticleSystem.MainModule main = particles.main;
            float speed = Mathf.Sqrt(scale);
            main.startSpeed = new ParticleSystem.MinMaxCurve(MinSpeed * speed, MaxSpeed * speed);
            main.startSize = new ParticleSystem.MinMaxCurve(chipSize * scale * 0.35f, chipSize * scale);
            main.startColor = new ParticleSystem.MinMaxGradient(burst.Color * 0.75f, burst.Color * 1.1f);

            var emit = new ParticleSystem.EmitParams { position = burst.Position, applyShapeToPosition = true };
            particles.Emit(emit, count);
        }

        // A lopsided octahedron: reads as a chip of rock at any angle, in eight triangles.
        private static Mesh CreateChipMesh()
        {
            var mesh = new Mesh { name = "Rock Chip" };
            mesh.vertices = new[]
            {
                new Vector3(0.5f, 0.05f, 0f), new Vector3(-0.42f, -0.04f, 0.06f), new Vector3(0.04f, 0.38f, -0.05f),
                new Vector3(-0.06f, -0.45f, 0.02f), new Vector3(0.02f, 0.03f, 0.33f), new Vector3(-0.03f, -0.02f, -0.4f)
            };
            mesh.triangles = new[] { 0, 2, 4, 2, 1, 4, 1, 3, 4, 3, 0, 4, 2, 0, 5, 1, 2, 5, 3, 1, 5, 0, 3, 5 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
