using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.Sectors.Far
{
    /// <summary>
    /// The planets a run's system is built from. Entries name their prefabs by Resources path rather than referencing them,
    /// so a scene that uses the catalogue loads only the planets its run picks; each planet's textures are about 17 MB.
    /// Built from Planet Forge's planets by <c>PlanetCatalogBuilder</c>, which keeps hand-tuned roles and weights.
    /// </summary>
    [CreateAssetMenu(menuName = "Dead Cargo/Planet Catalog", fileName = "PlanetCatalog")]
    public sealed class PlanetCatalog : ScriptableObject
    {
        [Tooltip("Every planet a run may use.")]
        [SerializeField] private List<PlanetEntry> entries = new List<PlanetEntry>();

        public IReadOnlyList<PlanetEntry> Entries => entries;
        public int Count => entries.Count;

        /// <summary>The entry with <paramref name="planetName"/>, or null.</summary>
        public PlanetEntry Find(string planetName)
        {
            foreach (PlanetEntry entry in entries)
            {
                if (entry != null && entry.Name == planetName)
                    return entry;
            }

            return null;
        }

        /// <summary>Replaces every entry. For the catalogue builder.</summary>
        public void SetEntries(IEnumerable<PlanetEntry> newEntries)
        {
            entries.Clear();
            if (newEntries != null)
                entries.AddRange(newEntries);
        }

        /// <summary>Loads the planet's prefab. Null when the entry names none, after a warning.</summary>
        public static GameObject Load(PlanetEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.ResourcePath))
                return null;

            return Checked(entry, Resources.Load<GameObject>(entry.ResourcePath));
        }

        /// <summary>
        /// Starts loading the planet's prefab in the background, textures and all. Null when the entry names none; read
        /// the prefab with <see cref="Loaded"/> once the request is done.
        /// </summary>
        public static ResourceRequest LoadAsync(PlanetEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.ResourcePath))
                return null;

            return Resources.LoadAsync<GameObject>(entry.ResourcePath);
        }

        /// <summary>The prefab a finished <see cref="LoadAsync"/> request brought in. Null when there is none, after a warning.</summary>
        public static GameObject Loaded(PlanetEntry entry, ResourceRequest request)
        {
            if (request == null)
                return null;

            return Checked(entry, request.asset as GameObject);
        }

        private static GameObject Checked(PlanetEntry entry, GameObject prefab)
        {
            if (prefab == null)
                Debug.LogWarning($"Planet '{entry.Name}' has no prefab at Resources/{entry.ResourcePath}. Build the planet catalogue again.");

            return prefab;
        }
    }
}
