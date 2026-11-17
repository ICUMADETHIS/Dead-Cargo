using System;
using UnityEngine;

namespace DeadCargo.Sectors.Far
{
    /// <summary>
    /// One planet in the catalogue: its prefab, named by its path under a Resources folder so only the run's planets are
    /// loaded, its radius in far-world units, the roles it may play and how often it is chosen.
    /// </summary>
    [Serializable]
    public sealed class PlanetEntry
    {
        [Tooltip("The planet's name, as shown in the God Menu.")]
        [SerializeField] private string name;

        [Tooltip("The prefab's path under a Resources folder, without the extension. Set by the catalogue builder.")]
        [SerializeField] private string resourcePath;

        [Tooltip("The landscape's radius, in far-world units (kilometres). Read from the prefab by the catalogue builder.")]
        [SerializeField, Min(0.01f)] private float radius = 500f;

        [Tooltip("The parts this planet may play in a run: the primary, a moon or a distant planet.")]
        [SerializeField] private BodyRoles roles = BodyRoles.All;

        [Tooltip("How often this planet is chosen, against the others that can play the same part. 0 never chooses it.")]
        [SerializeField, Min(0f)] private float weight = 1f;

        public PlanetEntry(string name, string resourcePath, float radius, BodyRoles roles, float weight)
        {
            this.name = name;
            this.resourcePath = resourcePath;
            this.radius = radius;
            this.roles = roles;
            this.weight = weight;
        }

        public string Name => name;
        public string ResourcePath => resourcePath;

        /// <summary>The landscape's radius, in far-world units.</summary>
        public float Radius => radius;

        public BodyRoles Roles => roles;
        public float Weight => weight;

        /// <summary>True when this planet may play <paramref name="role"/> and has a chance of being chosen.</summary>
        public bool CanPlay(BodyRoles role)
        {
            return (roles & role) != 0 && weight > 0f && radius > 0f;
        }
    }
}
