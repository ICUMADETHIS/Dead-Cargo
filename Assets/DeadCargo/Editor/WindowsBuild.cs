using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Builds the game for 64-bit Windows from the scenes enabled in the Build Settings, starting at the first, the
    /// mothership. The window and the save folder are named Dead Cargo for the build only: the project's product name is
    /// put back afterwards, so the editor keeps its own save data and the built game starts with a fresh profile. Run it
    /// from the menu, or headless through -executeMethod with an optional -buildPath.
    /// </summary>
    internal static class WindowsBuild
    {
        private const string MenuPath = "Dead Cargo/Build/Windows";
        private const string ProductName = "Dead Cargo";
        private const string DefaultFolder = "Builds/Windows";
        private const string PathArgument = "-buildPath";

        [MenuItem(MenuPath, priority = 300)]
        private static void BuildFromMenu()
        {
            BuildReport report = Build(DefaultPath());
            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(report.summary.outputPath);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanBuild()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>Headless entry point. Exits Unity with 0 when the build succeeds and 1 when it does not.</summary>
        public static void BuildFromCommandLine()
        {
            BuildReport report = Build(ArgumentAfter(PathArgument) ?? DefaultPath());
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static BuildReport Build(string path)
        {
            string[] scenes = EnabledScenes();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            string projectProductName = PlayerSettings.productName;
            PlayerSettings.productName = ProductName;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                Debug.Log($"[WindowsBuild] {summary.result}: {summary.outputPath}, {summary.totalSize / (1024 * 1024)} MB in " +
                    $"{summary.totalTime.TotalMinutes:0.0} min, {summary.totalErrors} errors, {summary.totalWarnings} warnings. " +
                    $"Scenes: {string.Join(", ", scenes)}");
                return report;
            }
            finally
            {
                PlayerSettings.productName = projectProductName;
            }
        }

        // Enabled scenes in Build Settings order, each once. The first is where the game starts.
        private static string[] EnabledScenes()
        {
            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !scenes.Contains(scene.path) && File.Exists(scene.path))
                    scenes.Add(scene.path);
            }

            return scenes.ToArray();
        }

        private static string DefaultPath()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(project, DefaultFolder, ProductName + ".exe"));
        }

        private static string ArgumentAfter(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; ++i)
            {
                if (args[i] == name)
                    return args[i + 1];
            }

            return null;
        }
    }
}
