using DeadCargo.EditorTools.Diagnostics;
using DeadCargo.Sectors;
using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Inspect the generated sector, change its seed, and switch its debug drawing on and off.</summary>
    internal sealed class SectorTab : GodMenuTab
    {
        private const float MetresPerKilometre = 1000f;

        private static readonly string[] SeedLabels = { "Random Seed", "Reset Seed" };
        private static readonly string[] ViewLabels = { "Show All", "Hide All" };
        private static readonly string[] TestLabels = { "Run Self-Test" };

        private SectorSpace space;
        private SectorDebugView view;

        public override string Title => "Sector";
        public override GodMenuGroup Group => GodMenuGroup.Run;
        public override int Order => 60;

        public override bool IsAvailable => space != null && space.Definition != null;

        public override string Summary => space != null && space.Definition != null
            ? $"{space.Definition.DisplayName}  ·  seed {space.Seed}"
            : null;

        public override void OnUpdate()
        {
            space = GodMenuContext.IsPlaying ? SectorSpace.Active : null;
            view = space != null ? space.GetComponent<SectorDebugView>() : null;
        }

        public override void OnGUI()
        {
            if (space == null || space.Definition == null)
                return;

            DrawSector();
            DrawSeed();
            DrawView();
            DrawSelfTest();
        }

        private void DrawSector()
        {
            using (var section = new GodMenuGUI.SectionScope("Sector"))
            {
                if (!section.Expanded)
                    return;

                SectorGrid grid = space.Grid;
                Vector3 size = grid.Size / MetresPerKilometre;
                Vector3 focus = space.FocusPosition;
                CellCoord cell = grid.CellAt(focus);
                float density = space.DensityAt(focus, out int regionIndex);

                GodMenuGUI.Field("Size", $"{size.x:0.#} × {size.y:0.#} × {size.z:0.#} km");
                GodMenuGUI.Field("Cells", $"{grid.CellCount:N0}  ·  {grid.CellSize:0} m each");
                GodMenuGUI.Field("Player Cell", grid.Contains(cell) ? cell.ToString() : $"{cell}  outside the sector");
                GodMenuGUI.Field("Region", regionIndex != SectorDefinition.NoRegion ? space.Definition.Regions[regionIndex].Name : "Open space");
                GodMenuGUI.Bar(density, $"Density here  {density:0.00}", GodMenuGUI.Accent);
            }
        }

        private void DrawSeed()
        {
            using (var section = new GodMenuGUI.SectionScope("Seed"))
            {
                if (!section.Expanded)
                    return;

                GodMenuGUI.Field("In Use", space.HasSeedOverride ? $"{space.Seed}  (override)" : $"{space.Seed}  (definition)");

                bool[] enabled = { true, space.HasSeedOverride };
                switch (GodMenuGUI.ButtonRow(SeedLabels, enabled))
                {
                    case 0:
                        space.SetSeed(unchecked((uint)Random.Range(int.MinValue, int.MaxValue)));
                        break;
                    case 1:
                        space.ResetSeed();
                        break;
                }
            }
        }

        private void DrawView()
        {
            using (var section = new GodMenuGUI.SectionScope("Debug View"))
            {
                if (!section.Expanded || view == null)
                    return;

                view.ShowBounds = EditorGUILayout.Toggle("Sector Bounds", view.ShowBounds);
                view.ShowRegions = EditorGUILayout.Toggle("Regions", view.ShowRegions);
                view.ShowCells = EditorGUILayout.Toggle("Cells", view.ShowCells);
                view.ShowSamples = EditorGUILayout.Toggle("Sample Points", view.ShowSamples);
                view.CellRadius = EditorGUILayout.IntSlider("Cell Radius", view.CellRadius, 0, SectorDebugView.MaxCellRadius);
                view.SamplesPerCell = EditorGUILayout.IntSlider("Samples Per Cell", view.SamplesPerCell, 1, SectorDebugView.MaxSamplesPerCell);

                if (view.ShowSamples)
                    GodMenuGUI.Field("Points Drawn", view.AcceptedSamples.ToString("N0"));

                switch (GodMenuGUI.ButtonRow(ViewLabels))
                {
                    case 0:
                        SetAll(true);
                        break;
                    case 1:
                        SetAll(false);
                        break;
                }
            }
        }

        private static void DrawSelfTest()
        {
            using (var section = new GodMenuGUI.SectionScope("Self-Test"))
            {
                if (!section.Expanded)
                    return;

                if (SectorDiagnostics.LastSummary != null)
                    GodMenuGUI.Status(SectorDiagnostics.LastPassed, SectorDiagnostics.LastSummary + "  ·  report in the Console");

                if (GodMenuGUI.ButtonRow(TestLabels) == 0)
                    SectorDiagnostics.Run();
            }
        }

        private void SetAll(bool visible)
        {
            view.ShowBounds = visible;
            view.ShowRegions = visible;
            view.ShowCells = visible;
            view.ShowSamples = visible;
        }
    }
}
