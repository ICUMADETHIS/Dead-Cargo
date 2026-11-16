using UnityEngine;
using UnityEngine.Rendering;

namespace DeadCargo.Destruction
{
    /// <summary>
    /// Rock chips for every cut in the scene, from one particle system: no objects are spawned, and every chip in the
    /// scene draws in one batch. Each cut is one emission; the particle system scatters, sizes and spins the chips
    /// itself. Chips fly out of the cut, tumble, shrink and vanish. A per-frame budget keeps heavy fire cheap.
    /// </summary>
    internal sealed class DebrisParticles
    {
        private const float MinSpeed = 2f;
        private const float MaxSpeed = 9f;
        private const float SpreadAngle = 55f;
        private const float MaxSpin = 7f;

        private readonly ParticleSystem particles;
        private readonly float chipsPerCubicMetre;
        private readonly int maxChipsPerBurst;
        private readonly int maxChipsPerFrame;
        private readonly float chipSize;
        private int frame = -1;
        private int emittedThisFrame;

        public DebrisParticles(Transform parent, Material material, int maxChips, float chipsPerCubicMetre, int maxChipsPerBurst,
            int maxChipsPerFrame, float chipSize)
        {
            this.chipsPerCubicMetre = chipsPerCubicMetre;
            this.maxChipsPerBurst = maxChipsPerBurst;
            this.maxChipsPerFrame = maxChipsPerFrame;
            this.chipSize = chipSize;

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
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
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

        /// <summary>Throws chips out of a cut.</summary>
        /// <param name="volume">Rock removed, in cubic metres; sets how many chips fly.</param>
        /// <param name="radius">Size of the cut, in metres; sets how far apart and how big the chips are.</param>
        public void Burst(Vector3 position, Vector3 normal, Color color, float volume, float radius)
        {
            if (particles == null || volume <= 0f)
                return;

            if (frame != Time.frameCount)
            {
                frame = Time.frameCount;
                emittedThisFrame = 0;
            }

            int count = Mathf.Min(Mathf.Clamp(Mathf.RoundToInt(volume * chipsPerCubicMetre), 1, maxChipsPerBurst),
                maxChipsPerFrame - emittedThisFrame);
            if (count <= 0)
                return;

            emittedThisFrame += count;
            float scale = Mathf.Clamp(radius, 0.4f, 3f);
            if (normal.sqrMagnitude < 0.5f)
                normal = Vector3.up;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.rotation = Quaternion.LookRotation(normal).eulerAngles;
            shape.radius = radius * 0.5f;

            ParticleSystem.MainModule main = particles.main;
            float speed = Mathf.Sqrt(scale);
            main.startSpeed = new ParticleSystem.MinMaxCurve(MinSpeed * speed, MaxSpeed * speed);
            main.startSize = new ParticleSystem.MinMaxCurve(chipSize * scale * 0.35f, chipSize * scale);
            main.startColor = new ParticleSystem.MinMaxGradient(color * 0.75f, color * 1.1f);

            var emit = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
            particles.Emit(emit, count);
        }

        public void Clear()
        {
            if (particles != null)
                particles.Clear();
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
