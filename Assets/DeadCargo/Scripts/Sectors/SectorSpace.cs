using System;
using System.Collections.Generic;
using DeadCargo.Flow;
using DeadCargo.Sectors.Placement;
using Unity.Mathematics;
using UnityEngine;
using VSX.UniversalVehicleCombat;
using Object = UnityEngine.Object;

namespace DeadCargo.Sectors
{
    /// <summary>
    /// The generated space of the loaded sector: its definition, the run's seed, layout and cell grid, and the
    /// conversion between world space and sector space. Generators read everything they need from here.
    /// <para>
    /// <b>A new sector every load.</b> Unless the definition pins its seed, each load draws a new one. The seed varies
    /// the regions (<see cref="SectorLayout"/>) and places every <see cref="SectorPlacement"/> in the scene, such as
    /// extraction points and pickups, before any other script wakes. Changing the seed in Play Mode does both again.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class SectorSpace : MonoBehaviour
    {
        [Tooltip("The sector this scene generates.")]
        [SerializeField] private SectorDefinition definition;

        // Scene content placed by the run, in placement order, with the spots taken this run in sector space.
        private readonly List<SectorPlacement> placements = new List<SectorPlacement>();
        private readonly List<Vector3> taken = new List<Vector3>();

        private bool hasRunSeed;
        private uint seed;

        /// <summary>The sector space for the loaded scene, or null.</summary>
        public static SectorSpace Active { get; private set; }

        /// <summary>Raised when the seed changes, after the layout is rebuilt and content placed again.</summary>
        public event Action SeedChanged;

        public SectorDefinition Definition => definition;
        public SectorGrid Grid { get; private set; }

        /// <summary>The run's regions: the definition's, varied by the seed, plus the clearings placed content opens.</summary>
        public SectorLayout Layout { get; } = new SectorLayout();

        /// <summary>The seed in use. Outside Play Mode, the definition's.</summary>
        public uint Seed => hasRunSeed ? seed : DefinitionSeed;

        /// <summary>The seed this load started with.</summary>
        public uint LoadSeed { get; private set; }

        /// <summary>True when the seed has been changed since the load, by a debug tool.</summary>
        public bool HasSeedOverride => hasRunSeed && seed != LoadSeed;

        /// <summary>Scene content this run placed.</summary>
        public IReadOnlyList<SectorPlacement> Placements => placements;

        /// <summary>How many placed objects had to settle for the closest miss of their rule this run.</summary>
        public int MissedPlacements { get; private set; }

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

        /// <summary>
        /// Where the near world sits in the star system, in metres. System space is the star system's frame, with its
        /// origin at the sector's arrival point, so sector space is part of it. Zero while the player is in the sector;
        /// the cruise drive carries it as the player travels. Kept in double precision, as planets lie thousands of
        /// kilometres out.
        /// </summary>
        public double3 SystemOffset { get; private set; }

        public Vector3 WorldToSector(Vector3 worldPosition)
        {
            return worldPosition - OriginOffset;
        }

        public Vector3 SectorToWorld(Vector3 sectorPosition)
        {
            return sectorPosition + OriginOffset;
        }

        /// <summary>A world position's place in the star system, in metres.</summary>
        public double3 WorldToSystem(Vector3 worldPosition)
        {
            return (double3)(float3)WorldToSector(worldPosition) + SystemOffset;
        }

        /// <summary>
        /// A place in the star system as a world position. Far from the near world it is only good for directions,
        /// such as screen markers.
        /// </summary>
        public Vector3 SystemToWorld(double3 systemPosition)
        {
            return SectorToWorld((float3)(systemPosition - SystemOffset));
        }

        /// <summary>Moves the near world to a new place in the star system. For the cruise drive and debug tools.</summary>
        public void SetSystemOffset(double3 offset)
        {
            SystemOffset = offset;
        }

        /// <summary>The seed for one cell. <paramref name="stream"/> separates independent uses of the same cell.</summary>
        public uint CellSeed(CellCoord cell, uint stream = 0u)
        {
            return SectorHash.Cell(Seed, cell, stream);
        }

        /// <summary>
        /// How full space is at a sector-space position, from 0 to 1, and the index of the layout region that decides
        /// the content there, or <see cref="SectorDefinition.NoRegion"/>.
        /// </summary>
        public float DensityAt(Vector3 sectorPosition, out int regionIndex)
        {
            if (hasRunSeed)
                return Layout.DensityAt(sectorPosition, out regionIndex);

            if (definition != null)
                return definition.DensityAt(sectorPosition, out regionIndex);

            regionIndex = SectorDefinition.NoRegion;
            return 0f;
        }

        /// <summary>Generates from a different seed: a new layout, content placed again, then <see cref="SeedChanged"/>.</summary>
        public void SetSeed(uint newSeed)
        {
            if (!hasRunSeed || seed == newSeed)
                return;

            seed = newSeed;
            BuildRun();
            SeedChanged?.Invoke();
        }

        /// <summary>Returns to the seed this load started with.</summary>
        public void ResetSeed()
        {
            SetSeed(LoadSeed);
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
            CollectPlacements();

            LoadSeed = ChooseLoadSeed();
            seed = LoadSeed;
            hasRunSeed = true;
            BuildRun();
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

        private uint ChooseLoadSeed()
        {
            if (definition == null || !definition.NewSeedEachLoad)
                return DefinitionSeed;

            return SectorHash.Mix(unchecked((uint)DateTime.UtcNow.Ticks ^ (uint)Environment.TickCount ^ (uint)GetInstanceID()));
        }

        private void BuildRun()
        {
            using LoadScope measure = LoadReport.Measure("Sector layout");
            Layout.Build(definition != null ? definition.Regions : null, Grid, seed, definition != null && definition.VaryLayout);
            PlaceContent();
        }

        // Placement order must not depend on load order, so content is ordered by its rule, then by where it was
        // authored. Anything spawned later, such as jettisoned cargo, is never placed.
        private void CollectPlacements()
        {
            placements.Clear();
            SectorPlacement[] found = Object.FindObjectsByType<SectorPlacement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (SectorPlacement placement in found)
            {
                if (placement.gameObject.scene == gameObject.scene && placement.Rule != null)
                    placements.Add(placement);
            }

            placements.Sort(ComparePlacements);
        }

        private static int ComparePlacements(SectorPlacement a, SectorPlacement b)
        {
            int byOrder = a.Rule.Order.CompareTo(b.Rule.Order);
            if (byOrder != 0)
                return byOrder;

            Vector3 pa = a.transform.position;
            Vector3 pb = b.transform.position;
            int byPosition = pa.x != pb.x ? pa.x.CompareTo(pb.x) : pa.y != pb.y ? pa.y.CompareTo(pb.y) : pa.z.CompareTo(pb.z);
            return byPosition != 0 ? byPosition : string.CompareOrdinal(a.name, b.name);
        }

        // Each piece is placed against the layout as it stands, clearings opened by earlier pieces included, then opens
        // its own. A piece collected or destroyed since keeps its index, so the rest keep their spots.
        private void PlaceContent()
        {
            taken.Clear();
            MissedPlacements = 0;

            for (int i = 0; i < placements.Count; ++i)
            {
                SectorPlacement placement = placements[i];
                if (placement == null)
                    continue;

                PlacementSettings settings = placement.Rule.Settings;
                bool met = SectorPlacer.TryPlace(Layout.Set, Grid, seed, i, settings, taken, out Vector3 spot, out float heading);
                if (!met)
                    ++MissedPlacements;

                placement.Place(SectorToWorld(spot), heading, met);
                taken.Add(spot);
                Layout.AddClearing(placement.name, spot, settings.KeepClear);
            }

            if (MissedPlacements > 0)
            {
                Debug.LogWarning($"{MissedPlacements} placed object(s) in '{gameObject.scene.name}' found no spot that meets every part " +
                                 $"of their rule with seed {seed}, so each took the closest. Loosen the rule if this happens often.", this);
            }
        }
    }
}
