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
        public const int NoRegion = -1;

        private const float MinSize = 1000f;
        private const float MinCellSize = 100f;

        [Tooltip("Name shown to the player.")]
        [SerializeField] private string displayName = "Sector";

        [Tooltip("The same seed always generates the same sector.")]
        [SerializeField] private int seed = 1;

        [Tooltip("Size of the sector along each axis, in metres. The sector is centred on the origin.")]
        [SerializeField] private Vector3 size = new Vector3(40000f, 6000f, 40000f);

        [Tooltip("Edge length of a generation cell, in metres. Cells load and unload around the ship.")]
        [SerializeField, Min(MinCellSize)] private float cellSize = 1000f;

        [Tooltip("Fields and clearings. Where fields overlap, the strongest one decides the content.")]
        [SerializeField] private SectorRegion[] regions = Array.Empty<SectorRegion>();

        public string DisplayName => displayName;
        public int Seed => seed;

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
            regionIndex = NoRegion;
            float density = 0f;
            float cleared = 0f;

            for (int i = 0; i < regions.Length; ++i)
            {
                float strength = regions[i].StrengthAt(sectorPosition);
                if (strength <= 0f)
                    continue;

                if (regions[i].Kind == SectorRegionKind.Clearing)
                {
                    cleared = Mathf.Max(cleared, strength);
                }
                else if (strength > density)
                {
                    density = strength;
                    regionIndex = i;
                }
            }

            density *= 1f - cleared;
            if (density <= 0f)
                regionIndex = NoRegion;

            return density;
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
