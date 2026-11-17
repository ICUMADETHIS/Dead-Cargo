using DDT;
using UnityEngine;

namespace DeadCargo.Sectors
{
    /// <summary>
    /// Draws the sector's structure in the game view: its bounds, its regions, the cells around the player,
    /// and sample points showing where content would be generated. Drawn with Draw Debug Tools, whose calls
    /// are compiled out of release builds. In the editor, outside Play Mode, the bounds and regions show as gizmos.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SectorSpace))]
    public sealed class SectorDebugView : MonoBehaviour
    {
        /// <summary>Largest value of <see cref="CellRadius"/>. Keeps the view to a few thousand lines.</summary>
        public const int MaxCellRadius = 3;

        /// <summary>Largest value of <see cref="SamplesPerCell"/>.</summary>
        public const int MaxSamplesPerCell = 64;

        // Separates sample points from every other use of a cell's seed.
        private const uint SampleStream = 0xDEB06u;

        private const int EllipseSegments = 48;
        private const float SamplePointSize = 14f;

        // Label size per metre of distance from the camera, so labels read the same near and far.
        private const float LabelScale = 0.012f;

        private static readonly Color BoundsColor = new Color(0.45f, 0.8f, 1f);
        private static readonly Color CellColor = new Color(0.35f, 0.45f, 0.5f);
        private static readonly Color FocusCellColor = new Color(1f, 1f, 1f);
        private static readonly Color AsteroidColor = new Color(0.94f, 0.74f, 0.44f);
        private static readonly Color DebrisColor = new Color(0.7f, 0.53f, 1f);
        private static readonly Color ClearingColor = new Color(0.49f, 0.89f, 0.62f);

        [Tooltip("Outline of the whole sector.")]
        [SerializeField] private bool showBounds;

        [Tooltip("Outline and name of each region.")]
        [SerializeField] private bool showRegions;

        [Tooltip("Cells around the player.")]
        [SerializeField] private bool showCells;

        [Tooltip("Points where content would be generated in the cells around the player.")]
        [SerializeField] private bool showSamples;

        [Tooltip("How many cells out from the player's cell are drawn.")]
        [SerializeField, Range(0, MaxCellRadius)] private int cellRadius = 1;

        [Tooltip("Candidate points tested in each cell. More points show the density more clearly.")]
        [SerializeField, Range(1, MaxSamplesPerCell)] private int samplesPerCell = 16;

        private SectorSpace space;
        private string[] regionLabels;
        private uint labelledSeed;

        public bool ShowBounds { get => showBounds; set => showBounds = value; }
        public bool ShowRegions { get => showRegions; set => showRegions = value; }
        public bool ShowCells { get => showCells; set => showCells = value; }
        public bool ShowSamples { get => showSamples; set => showSamples = value; }

        public int CellRadius
        {
            get => cellRadius;
            set => cellRadius = Mathf.Clamp(value, 0, MaxCellRadius);
        }

        public int SamplesPerCell
        {
            get => samplesPerCell;
            set => samplesPerCell = Mathf.Clamp(value, 1, MaxSamplesPerCell);
        }

        /// <summary>Sample points accepted in the last frame drawn. Zero while samples are hidden.</summary>
        public int AcceptedSamples { get; private set; }

        private void Awake()
        {
            space = GetComponent<SectorSpace>();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD || DDT_ENABLED
        private void Update()
        {
            AcceptedSamples = 0;
            SectorDefinition definition = space.Definition;
            if (definition == null)
                return;

            if (showBounds)
                DrawDebugTools.DrawBox(space.SectorToWorld(Vector3.zero), Quaternion.identity, space.Grid.Size, BoundsColor);

            if (showRegions)
                DrawRegions();

            if (showCells || showSamples)
                DrawCells();
        }

        // The run's layout, as varied by the seed, with the clearings round placed content.
        private void DrawRegions()
        {
            SectorLayout layout = space.Layout;
            EnsureLabels(layout);

            Camera view = Camera.main;
            Vector3 eye = view != null ? view.transform.position : Vector3.zero;

            for (int i = 0; i < layout.Count; ++i)
            {
                RegionVolume region = layout[i];
                Vector3 centre = space.SectorToWorld(region.Centre);
                Vector3 right = region.RotateToSector(new Vector3(region.Radii.x, 0f, 0f));
                Vector3 forward = region.RotateToSector(new Vector3(0f, 0f, region.Radii.z));
                Vector3 up = Vector3.up * region.Radii.y;
                Color color = KindColor(region.Kind);

                DrawEllipse(centre, right, forward, color);
                DrawEllipse(centre, right, up, color);
                DrawEllipse(centre, forward, up, color);

                float size = Mathf.Max(1f, Vector3.Distance(eye, centre) * LabelScale);
                DrawDebugTools.DrawString3D(centre, regionLabels[i], TextAnchor.MiddleCenter, color, size);
            }
        }

        private void DrawCells()
        {
            SectorGrid grid = space.Grid;
            CellCoord focus = space.FocusCell;
            Vector3 cellSize = new Vector3(grid.CellSize, grid.CellSize, grid.CellSize);

            for (int x = focus.X - cellRadius; x <= focus.X + cellRadius; ++x)
            {
                for (int y = focus.Y - cellRadius; y <= focus.Y + cellRadius; ++y)
                {
                    for (int z = focus.Z - cellRadius; z <= focus.Z + cellRadius; ++z)
                    {
                        var cell = new CellCoord(x, y, z);
                        if (!grid.Contains(cell))
                            continue;

                        if (showCells)
                        {
                            DrawDebugTools.DrawBox(space.SectorToWorld(grid.CellCentre(cell)), Quaternion.identity, cellSize,
                                cell == focus ? FocusCellColor : CellColor);
                        }

                        if (showSamples)
                            DrawSamples(grid.CellMin(cell), cellSize, space.CellSeed(cell, SampleStream));
                    }
                }
            }
        }

        // A candidate point is kept when a random roll falls under the density there, which is the rule real
        // content will use. Every candidate draws the same number of random values, kept or not, so one point
        // never shifts the next.
        private void DrawSamples(Vector3 cellMin, Vector3 cellSize, uint cellSeed)
        {
            SectorLayout layout = space.Layout;
            var random = new SectorRandom(cellSeed);
            for (int i = 0; i < samplesPerCell; ++i)
            {
                Vector3 position = random.NextInBox(cellMin, cellSize);
                float roll = random.NextFloat();

                float density = layout.DensityAt(position, out int regionIndex);
                if (roll >= density)
                    continue;

                ++AcceptedSamples;
                DrawDebugTools.DrawPoint(space.SectorToWorld(position), SamplePointSize, KindColor(layout[regionIndex].Kind));
            }
        }

        private static void DrawEllipse(Vector3 centre, Vector3 axisA, Vector3 axisB, Color color)
        {
            Vector3 previous = centre + axisA;
            for (int i = 1; i <= EllipseSegments; ++i)
            {
                float angle = i * (2f * Mathf.PI / EllipseSegments);
                Vector3 next = centre + axisA * Mathf.Cos(angle) + axisB * Mathf.Sin(angle);
                DrawDebugTools.DrawLine(previous, next, color);
                previous = next;
            }
        }

        // Labels are built once per layout, so drawing them every frame allocates nothing.
        private void EnsureLabels(SectorLayout layout)
        {
            if (regionLabels != null && labelledSeed == space.Seed && regionLabels.Length == layout.Count)
                return;

            labelledSeed = space.Seed;
            regionLabels = new string[layout.Count];
            for (int i = 0; i < regionLabels.Length; ++i)
                regionLabels[i] = $"{layout.NameOf(i)}\n{KindName(layout[i].Kind)}";
        }
#endif

        private static Color KindColor(SectorRegionKind kind)
        {
            switch (kind)
            {
                case SectorRegionKind.DebrisField:
                    return DebrisColor;
                case SectorRegionKind.Clearing:
                    return ClearingColor;
                default:
                    return AsteroidColor;
            }
        }

        private static string KindName(SectorRegionKind kind)
        {
            switch (kind)
            {
                case SectorRegionKind.DebrisField:
                    return "DEBRIS FIELD";
                case SectorRegionKind.Clearing:
                    return "CLEARING";
                default:
                    return "ASTEROID FIELD";
            }
        }

#if UNITY_EDITOR
        // Edit-time preview: the game-view drawing only runs in Play Mode.
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying)
                return;

            SectorDefinition definition = GetComponent<SectorSpace>().Definition;
            if (definition == null)
                return;

            Gizmos.color = BoundsColor;
            Gizmos.DrawWireCube(Vector3.zero, definition.CreateGrid().Size);

            for (int i = 0; i < definition.Regions.Count; ++i)
            {
                SectorRegion region = definition.Regions[i];
                Gizmos.color = KindColor(region.Kind);
                Gizmos.matrix = Matrix4x4.TRS(region.Centre, Quaternion.Euler(0f, region.Yaw, 0f), region.Radii);
                Gizmos.DrawWireSphere(Vector3.zero, 1f);
            }

            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
