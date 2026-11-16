using System.Collections.Generic;
using DeadCargo.Sectors;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools.Diagnostics
{
    /// <summary>
    /// Runs the sector self-test from the editor and reports it to the Console. With a sector open,
    /// it also checks that sector's definition and estimates how much content it will generate.
    /// </summary>
    internal static class SectorDiagnostics
    {
        public const string MenuPath = "Dead Cargo/Diagnostics/Sector Self-Test";

        // Candidate points tested per cell when estimating content. Matches the debug view's upper limit.
        private const int EstimateCandidatesPerCell = SectorDebugView.MaxSamplesPerCell;
        private const int EstimatePoints = 200000;
        private const uint EstimateSeed = 0x5EC7012u;

        /// <summary>One line describing the last run, or null before the first.</summary>
        public static string LastSummary { get; private set; }

        public static bool LastPassed { get; private set; }

        [MenuItem(MenuPath, priority = 300)]
        private static void RunFromMenu()
        {
            Run();
        }

        /// <summary>Runs every check and logs the report. Returns true when all of them pass.</summary>
        public static bool Run()
        {
            SectorSelfTest.Result result = SectorSelfTest.Run();

            SectorDefinition definition = FindOpenDefinition();
            if (definition != null)
                CheckDefinition(definition, result);
            else
                result.Note("Sector definition", "no sector space in the open scene, so only the maths was checked");

            LastPassed = result.Passed;
            LastSummary = result.Passed
                ? $"{result.Checks} checks passed"
                : $"{result.Failures.Count} of {result.Checks} checks failed";

            string report = $"Sector self-test: {LastSummary}.\n{result.Report}";
            if (result.Passed)
                Debug.Log(report, definition);
            else
                Debug.LogError(report, definition);

            return result.Passed;
        }

        private static SectorDefinition FindOpenDefinition()
        {
            if (SectorSpace.Active != null && SectorSpace.Active.Definition != null)
                return SectorSpace.Active.Definition;

            List<SectorSpace> spaces = SceneSetupUtility.FindInScene<SectorSpace>(SceneManager.GetActiveScene());
            return spaces.Count > 0 ? spaces[0].Definition : null;
        }

        private static void CheckDefinition(SectorDefinition definition, SectorSelfTest.Result result)
        {
            SectorGrid grid = definition.CreateGrid();
            Vector3 gridMax = grid.Min + grid.Size;
            string label = $"'{definition.DisplayName}'";

            for (int i = 0; i < definition.Regions.Count; ++i)
            {
                SectorRegion region = definition.Regions[i];
                string name = string.IsNullOrEmpty(region.Name) ? $"Region {i}" : region.Name;

                result.Check(region.Radii.x > 0f && region.Radii.y > 0f && region.Radii.z > 0f, $"{name} has a size",
                    $"radii {region.Radii.x:0} x {region.Radii.y:0} x {region.Radii.z:0} m");
                result.Check(region.StrengthAt(region.Centre) > 0f, $"{name} has a density", "greater than zero at its centre");

                // Content is only generated inside the grid, so anything hanging outside is silently lost.
                Vector3 low = region.Centre - region.Radii;
                Vector3 high = region.Centre + region.Radii;
                bool inside = low.x >= grid.Min.x && low.y >= grid.Min.y && low.z >= grid.Min.z &&
                    high.x <= gridMax.x && high.y <= gridMax.y && high.z <= gridMax.z;
                result.Check(inside, $"{name} fits in the sector", inside ? "inside the bounds" : "extends past the bounds, where nothing is generated");
            }

            EstimateContent(definition, grid, label, result);
        }

        // Samples the whole sector to find how full it is, then scales that up to a count of generated objects.
        private static void EstimateContent(SectorDefinition definition, SectorGrid grid, string label, SectorSelfTest.Result result)
        {
            var random = new SectorRandom(EstimateSeed);
            var perRegion = new double[definition.Regions.Count];
            double total = 0d;
            bool valid = true;

            for (int i = 0; i < EstimatePoints; ++i)
            {
                float density = definition.DensityAt(random.NextInBox(grid.Min, grid.Size), out int regionIndex);
                valid &= density >= 0f && density <= 1f && (density > 0f) == (regionIndex != SectorDefinition.NoRegion);
                if (regionIndex == SectorDefinition.NoRegion)
                    continue;

                total += density;
                perRegion[regionIndex] += density;
            }

            result.Check(valid, $"{label} density is well formed", "always between 0 and 1, and names a region exactly when it is above 0");

            double candidates = (double)grid.CellCount * EstimateCandidatesPerCell;
            double objects = total / EstimatePoints * candidates;
            result.Note($"{label} content", $"about {objects:N0} objects at {EstimateCandidatesPerCell} candidates per cell, " +
                $"average density {total / EstimatePoints:0.000} over {grid.CellCount:N0} cells");

            for (int i = 0; i < perRegion.Length; ++i)
            {
                if (definition.Regions[i].Kind != SectorRegionKind.Clearing)
                    result.Note($"  {definition.Regions[i].Name}", $"about {perRegion[i] / EstimatePoints * candidates:N0} objects");
            }
        }
    }
}
