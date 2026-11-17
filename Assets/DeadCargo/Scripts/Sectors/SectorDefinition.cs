using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.Sectors
{
    /// <summary>
    /// Everything needed to generate one raid location: its seed, its size, its cell size and its regions.
    /// The sector's content is computed from this asset, so nothing generated is ever saved.
    /// </summary>
    [CreateAssetMenu(fileName = "Sector", menuName = "Dead Cargo/Sector Definition")]
    public sealed class SectorDefinition : ScriptableObject
    {
        /// <summary>Returned by <see cref="DensityAt"/> when a position is in no field.</summary>
        public const int NoRegion = SectorDensity.NoRegion;

        private const float MinSize = 1000f;
        private const float MinCellSize = 100f;

        [Tooltip("Name shown to the player.")]
        [SerializeField] private string displayName = "Sector";

        [Tooltip("The same seed always generates the same sector. Used every load while New Seed Each Load is off.")]
        [SerializeField] private int seed = 1;

        [Tooltip("Draw a new seed every load, so every run is a new sector. Off pins the seed above, to replay a layout.")]
        [SerializeField] private bool newSeedEachLoad = true;

        [Tooltip("Vary each region within its own limits every run. Off keeps every region exactly as authored.")]
        [SerializeField] private bool varyLayout = true;

        [Tooltip("Size of the sector along each axis, in metres. The sector is centred on the origin.")]
        [SerializeField] private Vector3 size = new Vector3(40000f, 6000f, 40000f);

        [Tooltip("Edge length of a generation cell, in metres. Cells load and unload around the ship.")]
        [SerializeField, Min(MinCellSize)] private float cellSize = 1000f;

        [Tooltip("Fields and clearings. Where fields overlap, the strongest one decides the content.")]
        [SerializeField] private SectorRegion[] regions = Array.Empty<SectorRegion>();

        public string DisplayName => displayName;
        public int Seed => seed;

        /// <summary>True when every load draws a new seed. False pins <see cref="Seed"/>.</summary>
        public bool NewSeedEachLoad => newSeedEachLoad;

        /// <summary>True when each run varies the regions within their limits.</summary>
        public bool VaryLayout => varyLayout;

        /// <summary>Size along each axis, in metres.</summary>
        public Vector3 Size => size;

        /// <summary>Edge length of a cell, in metres.</summary>
        public float CellSize => cellSize;

        public IReadOnlyList<SectorRegion> Regions => regions;

        /// <summary>Builds the cell grid that covers this sector.</summary>
        public SectorGrid CreateGrid()
        {
            return new SectorGrid(size, cellSize);
        }

        /// <summary>
        /// How full space is at a sector-space position, from 0 (empty) to 1.
        /// <paramref name="regionIndex"/> is the field that decides the content there, or <see cref="NoRegion"/>.
        /// </summary>
        public float DensityAt(Vector3 sectorPosition, out int regionIndex)
        {
            return SectorDensity.DensityAt(new SectorRegionSet(regions), sectorPosition, out regionIndex);
        }

        /// <summary>Sets every value at once. For setup tools that create a definition.</summary>
        public void Configure(string newDisplayName, int newSeed, Vector3 newSize, float newCellSize, SectorRegion[] newRegions)
        {
            displayName = newDisplayName;
            seed = newSeed;
            size = newSize;
            cellSize = newCellSize;
            regions = newRegions ?? Array.Empty<SectorRegion>();
            OnValidate();
        }

        private void OnValidate()
        {
            size = Vector3.Max(size, new Vector3(MinSize, MinSize, MinSize));
            cellSize = Mathf.Max(MinCellSize, cellSize);
            if (regions == null)
                regions = Array.Empty<SectorRegion>();
        }
    }
}
