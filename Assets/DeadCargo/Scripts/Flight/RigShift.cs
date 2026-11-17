using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.Flight
{
    /// <summary>
    /// Moves a whole object, such as the player's ship or the camera rig, to a new place in the world in one step: its
    /// rigidbody, its transform, and the world-space particles it carries, which keep their place around it. Trails are
    /// cleared rather than drawn across the jump. For rare moves, such as leaving or returning to the sector, so it may
    /// allocate.
    /// </summary>
    public static class RigShift
    {
        private static readonly List<TrailRenderer> Trails = new List<TrailRenderer>();
        private static readonly List<ParticleSystem> Particles = new List<ParticleSystem>();

        /// <summary>Moves <paramref name="root"/> by <paramref name="offset"/>, in metres.</summary>
        public static void Move(Transform root, Vector3 offset)
        {
            if (root == null || offset == Vector3.zero)
                return;

            if (root.TryGetComponent(out Rigidbody body))
                body.position += offset;

            root.position += offset;

            root.GetComponentsInChildren(true, Trails);
            foreach (TrailRenderer trail in Trails)
                trail.Clear();

            root.GetComponentsInChildren(true, Particles);
            foreach (ParticleSystem system in Particles)
            {
                if (system.main.simulationSpace != ParticleSystemSimulationSpace.World || system.particleCount == 0)
                    continue;

                var points = new ParticleSystem.Particle[system.particleCount];
                int count = system.GetParticles(points);
                for (int i = 0; i < count; ++i)
                    points[i].position += offset;

                system.SetParticles(points, count);
            }

            Trails.Clear();
            Particles.Clear();
        }
    }
}
