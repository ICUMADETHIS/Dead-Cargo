using DeadCargo.Destruction.Voxels;
using DeadCargo.Destruction.Voxels.Diagnostics;
using DeadCargo.DevTesting;
using DeadCargo.EditorTools.Destruction;
using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools.Diagnostics
{
    /// <summary>
    /// Runs the voxel core self-test from the editor and reports it to the Console. Needs no scene and works in or out
    /// of Play Mode. The in-game smoke test, which drives the whole pipeline, is in the God Menu Destruction tab.
    /// </summary>
    internal static class DestructionDiagnostics
    {
        public const string MenuPath = "Dead Cargo/Diagnostics/Destruction Self-Test";
        public const string SmokeTestMenuPath = "Dead Cargo/Diagnostics/Destruction Smoke Test (Play Mode)";

        /// <summary>One line describing the last run, or null before the first.</summary>
        public static string LastSummary { get; private set; }

        public static bool LastPassed { get; private set; }

        [MenuItem(MenuPath, priority = 301)]
        private static void RunFromMenu()
        {
            Run();
        }

        /// <summary>
        /// Starts the in-game smoke test in any scene. Creates the default asteroid types if they are missing; a scene
        /// with no Destruction System gets a temporary one for the run, so the scene itself is never changed.
        /// </summary>
        [MenuItem(SmokeTestMenuPath, priority = 302)]
        public static void RunSmokeTest()
        {
            if (!DestructionSetup.EnsureAssets(out string problem))
            {
                Debug.LogError("Destruction smoke test could not start: " + problem);
                return;
            }

            var ore = AssetDatabase.LoadAssetAtPath<AsteroidDefinition>(DestructionSetup.OreAsteroidPath);
            var rocky = AssetDatabase.LoadAssetAtPath<AsteroidDefinition>(DestructionSetup.RockyAsteroidPath);
            if (!DestructionSmokeTest.Begin(ore, rocky))
                Debug.LogWarning("Destruction smoke test is already running, or Play Mode is off.");
        }

        [MenuItem(SmokeTestMenuPath, true)]
        private static bool CanRunSmokeTest()
        {
            return EditorApplication.isPlaying && !DestructionSmokeTest.IsRunning;
        }

        /// <summary>Runs every check and logs the report. Returns true when all of them pass.</summary>
        public static bool Run()
        {
            VoxelSelfTest.Result result = VoxelSelfTest.Run();
            LastPassed = result.Passed;
            LastSummary = result.Passed
                ? $"{result.Checks} core checks passed"
                : $"{result.Failures.Count} of {result.Checks} core checks failed";

            string report = $"Destruction self-test: {LastSummary}.\n{result.Report}";
            if (result.Passed)
                Debug.Log(report);
            else
                Debug.LogError(report);

            return result.Passed;
        }
    }
}
