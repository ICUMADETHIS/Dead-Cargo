using System.Collections.Generic;
using DeadCargo.EditorTools.Diagnostics;
using DeadCargo.Sectors;
using DeadCargo.Sectors.Far;
using DeadCargo.Sectors.Field;
using DeadCargo.Sectors.Placement;
using DeadCargo.Visuals;
using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>
    /// Inspect the generated sector, its run layout and its asteroid field, change or pin its seed, and switch its debug
    /// drawing on and off.
    /// </summary>
    internal sealed class SectorTab : GodMenuTab
    {
        private const float MetresPerKilometre = 1000f;
        private const string PinLabel = "Pin Seed";
        private const string UnpinLabel = "Unpin";
        private const string SeedProperty = "seed";
        private const string NewSeedProperty = "newSeedEachLoad";

        private static readonly string[] ViewLabels = { "Show All", "Hide All" };
        private static readonly string[] TestLabels = { "Run Self-Test" };
        private static readonly string[] FieldLabels = { "Rebuild Field" };
        private static readonly string[] PlanetLabels = { "Rebuild Planets" };

        private readonly string[] seedLabels = { "Random Seed", "Reset Seed", PinLabel };
        private readonly bool[] seedEnabled = { true, false, true };
        private readonly List<SectorPlacementRule> rules = new List<SectorPlacementRule>();

        private SectorSpace space;
        private SectorDebugView view;
        private AsteroidField field;
        private FarSpace far;
        private BackdropBodies backdrop;

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
            field = space != null ? space.GetComponent<AsteroidField>() : null;
            far = space != null ? FarSpace.Active : null;
            backdrop = far != null ? far.GetComponent<BackdropBodies>() : null;
        }

        public override void OnGUI()
        {
            if (space == null || space.Definition == null)
                return;

            DrawSector();
            DrawSeed();
            DrawLayout();
            DrawPlanets();
            DrawField();
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
                GodMenuGUI.Field("Region", regionIndex != SectorDefinition.NoRegion ? space.Layout.NameOf(regionIndex) : "Open space");
                GodMenuGUI.Bar(density, $"Density here  {density:0.00}", GodMenuGUI.Accent);
            }
        }

        private void DrawField()
        {
            if (field == null)
                return;

            using (var section = new GodMenuGUI.SectionScope("Asteroid Field"))
            {
                if (!section.Expanded)
                    return;

                bool drawn = EditorGUILayout.Toggle("Draw Field", field.enabled);
                if (drawn != field.enabled)
                    field.enabled = drawn;

                string generating = field.IsGenerating ? "  ·  generating" : string.Empty;
                GodMenuGUI.Field("Cells", $"{field.LoadedCells} loaded of {field.SlotCount} slots{generating}");
                GodMenuGUI.Field("Rocks", $"{field.LoadedRocks:N0} loaded  ·  {field.DrawnRocks:N0} drawn");
                GodMenuGUI.Field("Draw Calls", field.DrawCalls.ToString("N0"));
                GodMenuGUI.Field("Main Thread", $"{field.MainThreadMilliseconds:0.00} ms");

                bool solid = EditorGUILayout.Toggle("Solid Rocks", field.SolidRocks);
                if (solid != field.SolidRocks)
                    field.SolidRocks = solid;

                GodMenuGUI.Field("Solid Now", field.SolidRockCount.ToString("N0"));
                GodMenuGUI.Bar(field.DensityHere, $"Field here  {field.DensityHere:0.00}  (clumps and gaps)", GodMenuGUI.Accent);

                if (GodMenuGUI.ButtonRow(FieldLabels) == 0)
                    field.Rebuild();

                GodMenuGUI.Hint("Rocks near the player are solid, large ones from further off. Range and the most at once are on " +
                                "the Asteroid Field component.");
            }
        }

        private void DrawSeed()
        {
            using (var section = new GodMenuGUI.SectionScope("Seed"))
            {
                if (!section.Expanded)
                    return;

                SectorDefinition definition = space.Definition;
                bool pinned = !definition.NewSeedEachLoad;
                string origin = space.HasSeedOverride ? "changed in Play Mode" : pinned ? "pinned" : "new this load";
                GodMenuGUI.Field("In Use", $"{space.Seed}  ({origin})");
                GodMenuGUI.Field("Next Load", pinned ? $"seed {definition.Seed} again" : "a new seed");

                seedLabels[2] = pinned && (uint)definition.Seed == space.Seed ? UnpinLabel : PinLabel;
                seedEnabled[1] = space.HasSeedOverride;
                switch (GodMenuGUI.ButtonRow(seedLabels, seedEnabled))
                {
                    case 0:
                        space.SetSeed(unchecked((uint)Random.Range(int.MinValue, int.MaxValue)));
                        break;
                    case 1:
                        space.ResetSeed();
                        break;
                    case 2:
                        if (seedLabels[2] == PinLabel)
                            Pin(definition, unchecked((int)space.Seed));
                        else
                            Unpin(definition);
                        break;
                }

                GodMenuGUI.Hint("Pin keeps this sector, its layout, content and sky, for every load until unpinned. The seed and the " +
                                "switch are on the sector definition.");
            }
        }

        // The run's regions as varied by the seed, and the content placed in them, measured from the arrival point.
        private void DrawLayout()
        {
            using (var section = new GodMenuGUI.SectionScope("Layout"))
            {
                if (!section.Expanded)
                    return;

                SectorLayout layout = space.Layout;
                for (int i = 0; i < layout.AuthoredCount; ++i)
                {
                    RegionVolume region = layout[i];
                    GodMenuGUI.Field(layout.NameOf(i), $"{Describe(region.Centre)}  ·  turned {region.Yaw:0}°");
                }

                rules.Clear();
                foreach (SectorPlacement placement in space.Placements)
                {
                    if (placement != null && !rules.Contains(placement.Rule))
                        rules.Add(placement.Rule);
                }

                foreach (SectorPlacementRule rule in rules)
                    DrawPlaced(rule);

                if (space.MissedPlacements > 0)
                    GodMenuGUI.Status(false, $"{space.MissedPlacements} placed with the closest miss of their rule");

                if (!space.Definition.VaryLayout)
                    GodMenuGUI.Hint("Vary Layout is off on the sector definition, so regions stay as authored.");
            }
        }

        private void DrawPlaced(SectorPlacementRule rule)
        {
            int count = 0;
            float nearest = float.MaxValue;
            Vector3 only = Vector3.zero;
            foreach (SectorPlacement placement in space.Placements)
            {
                if (placement == null || placement.Rule != rule)
                    continue;

                Vector3 spot = space.WorldToSector(placement.transform.position);
                ++count;
                only = spot;
                nearest = Mathf.Min(nearest, spot.magnitude);
            }

            GodMenuGUI.Field(ObjectNames.NicifyVariableName(rule.name), count == 1
                ? Describe(only)
                : $"{count} placed  ·  nearest {nearest / MetresPerKilometre:0.0} km");
        }

        // Distance from the arrival point and its bearing, clockwise from straight ahead.
        private static string Describe(Vector3 sectorPosition)
        {
            float bearing = Mathf.Repeat(Mathf.Atan2(sectorPosition.x, sectorPosition.z) * Mathf.Rad2Deg, 360f);
            return $"{sectorPosition.magnitude / MetresPerKilometre:0.0} km at {bearing:0}°";
        }

        // The far space switch and the run's planets: what each is, how large, where, and how far from the sun.
        private void DrawPlanets()
        {
            if (far == null)
                return;

            using (var section = new GodMenuGUI.SectionScope("Planets"))
            {
                if (!section.Expanded)
                    return;

                bool drawn = EditorGUILayout.Toggle("Far Space", far.enabled);
                if (drawn != far.enabled)
                    far.enabled = drawn;

                if (backdrop == null || backdrop.Plans.Count == 0)
                {
                    GodMenuGUI.Field("Bodies", far.IsValid ? "none this run" : "far space has no layer");
                    return;
                }

                Vector3 towardsSun = SceneSun.Direction;
                for (int i = 0; i < backdrop.Plans.Count; ++i)
                {
                    BackdropPlan plan = backdrop.Plans[i];
                    string missed = plan.MetRule ? string.Empty : "  ·  closest miss";
                    GodMenuGUI.Field($"{plan.Role}  ·  {backdrop.NameOf(i)}",
                        $"{plan.Size:0.#}° across  ·  {Vector3.Angle(plan.Direction, towardsSun):0}° from the sun  ·  {Bearing(plan.Direction)}{missed}");
                }

                GodMenuGUI.Field("Sun", $"{backdrop.SunRadius * 2f:0}° across with its corona");
                if (GodMenuGUI.ButtonRow(PlanetLabels) == 0)
                    backdrop.Rebuild();

                GodMenuGUI.Hint("Planets follow the run seed: Random Seed gives a new system. Their rules are on the Far Space object, " +
                                "the planets' roles and weights on Data/Planets/PlanetCatalog.");
            }
        }

        // Bearing clockwise from straight ahead, and height above the sector's plane.
        private static string Bearing(Vector3 direction)
        {
            float bearing = Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 360f);
            return $"bearing {bearing:0}°, {BackdropLayout.Elevation(direction):+0;-0;0}° up";
        }

        private static void Pin(SectorDefinition definition, int seed)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty(SeedProperty).intValue = seed;
            serialized.FindProperty(NewSeedProperty).boolValue = false;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(definition);
        }

        private static void Unpin(SectorDefinition definition)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty(NewSeedProperty).boolValue = true;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(definition);
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
