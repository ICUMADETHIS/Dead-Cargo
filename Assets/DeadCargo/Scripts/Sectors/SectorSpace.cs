using System;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Sectors
{
    /// <summary>
    /// The generated space of the loaded sector: its definition, seed and cell grid, and the conversion
    /// between world space and sector space. Generators read everything they need from here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SectorSpace : MonoBehaviour
    {
        [Tooltip("The sector this scene generates.")]
        [SerializeField] private SectorDefinition definition;

        private bool hasSeedOverride;
        private uint seedOverride;

        /// <summary>The sector space for the loaded scene, or null.</summary>
        public static SectorSpace Active { get; private set; }

        /// <summary>Raised when the seed changes, so generated content can be rebuilt.</summary>
        public event Action SeedChanged;

        public SectorDefinition Definition => definition;
        public SectorGrid Grid { get; private set; }

        /// <summary>The seed in use: the definition's, unless it has been overridden.</summary>
        public uint Seed => hasSeedOverride ? seedOverride : DefinitionSeed;

        public bool HasSeedOverride => hasSeedOverride;

        /// <summary>
        /// World-space position of the sector's origin. Zero until the floating origin moves the world, which it does not
        /// yet (see <see cref="DeadCargo.Flow.FloatingOriginGuard"/>), so always convert through
        /// <see cref="WorldToSector"/> and <see cref="SectorToWorld"/>.
        /// </summary>
        public Vector3 OriginOffset { get; private set; }

        /// <summary>
        /// Where content is generated around, in sector space: the player's vehicle, or the main camera without one.
        /// </summary>
        public Vector3 FocusPosition
        {
            get
            {
                GameAgentManager agents = GameAgentManager.Instance;
                GameAgent player = agents != null ? agents.FocusedGameAgent : null;
                if (player != null && player.Vehicle != null)
                    return WorldToSector(player.Vehicle.transform.position);

                Camera view = Camera.main;
                return view != null ? WorldToSector(view.transform.position) : Vector3.zero;
            }
        }

        /// <summary>The cell the focus is in. It may lie outside the grid when the player leaves the sector.</summary>
        public CellCoord FocusCell => Grid.CellAt(FocusPosition);

        private uint DefinitionSeed => definition != null ? unchecked((uint)definition.Seed) : 0u;

        public Vector3 WorldToSector(Vector3 worldPosition)
        {
            return worldPosition - OriginOffset;
        }

        public Vector3 SectorToWorld(Vector3 sectorPosition)
        {
            return sectorPosition + OriginOffset;
        }

        /// <summary>The seed for one cell. <paramref name="stream"/> separates independent uses of the same cell.</summary>
        public uint CellSeed(CellCoord cell, uint stream = 0u)
        {
            return SectorHash.Cell(Seed, cell, stream);
        }

        /// <summary>
        /// How full space is at a sector-space position, from 0 to 1, and the index of the region that decides
        /// the content there, or <see cref="SectorDefinition.NoRegion"/>.
        /// </summary>
        public float DensityAt(Vector3 sectorPosition, out int regionIndex)
        {
            if (definition != null)
                return definition.DensityAt(sectorPosition, out regionIndex);

            regionIndex = SectorDefinition.NoRegion;
            return 0f;
        }

        /// <summary>Generates from a different seed. For debug tools and per-run variation.</summary>
        public void SetSeed(uint seed)
        {
            if (hasSeedOverride && seedOverride == seed)
                return;

            hasSeedOverride = true;
            seedOverride = seed;
            SeedChanged?.Invoke();
        }

        /// <summary>Returns to the definition's seed.</summary>
        public void ResetSeed()
        {
            if (!hasSeedOverride)
                return;

            hasSeedOverride = false;
            SeedChanged?.Invoke();
        }

        /// <summary>Rebuilds the grid after the definition's size or cell size has been edited. For debug tools.</summary>
        public void RebuildGrid()
        {
            Grid = definition != null ? definition.CreateGrid() : default;
        }

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
        }

        private void Awake()
        {
            if (definition == null)
                Debug.LogWarning("Sector space has no sector definition, so nothing will be generated.", this);

            RebuildGrid();
        }

        private void OnEnable()
        {
            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
        }
    }
}
