using System;
using System.Collections.Generic;
using DeadCargo.Authority;
using DeadCargo.Cargo;
using DeadCargo.EditorTools.Destruction;
using DeadCargo.Extraction;
using DeadCargo.Heat;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools
{
    /// <summary>
    /// Turns the open scene into a playable sector by running every phase setup in order.
    /// Each step is safe to re-run, so this also repairs a scene that is missing a system.
    /// A phase that adds a sector system adds one step here.
    /// </summary>
    internal static class SectorSetup
    {
        public const string MenuPath = "Dead Cargo/Setup/Set Up Sector (Open Scene)";

        /// <summary>One system a sector needs: how to set it up, and how to tell it is already there.</summary>
        public sealed class Step
        {
            public Step(string label, Func<bool> apply, Func<Scene, bool> isInstalled)
            {
                Label = label;
                Apply = apply;
                IsInstalled = isInstalled;
            }

            public string Label { get; }
            public Func<bool> Apply { get; }
            public Func<Scene, bool> IsInstalled { get; }
        }

        /// <summary>The setup steps, in the order they must run. Later steps rely on earlier ones.</summary>
        public static readonly Step[] Steps =
        {
            new Step("Cargo", CargoPhaseSetup.Apply, Has<CargoHold>),
            new Step("Jettison", JettisonPhaseSetup.Apply, Has<CargoJettison>),
            new Step("Heat", HeatPhaseSetup.Apply, Has<HeatTracker>),
            new Step("Authority", AuthorityPhaseSetup.Apply, Has<AuthorityResponse>),
            new Step("Extraction", ExtractionPhaseSetup.Apply, scene => Has<RunTracker>(scene) && DarkMatterSetup.HasPickups(scene)),
            new Step("Extraction Point", ExtractionPointSetup.Apply, ExtractionPointSetup.IsInstalled),
            new Step("Sector Space", SectorSpaceSetup.Apply, SectorSpaceSetup.IsInstalled),
            new Step("Destruction", DestructionSetup.Apply, DestructionSetup.IsInstalled),
            new Step("Asteroid Field", AsteroidFieldSetup.Apply, AsteroidFieldSetup.IsInstalled),
            new Step("Content Placement", SectorPlacementSetup.Apply, SectorPlacementSetup.IsInstalled),
            new Step("Planet Flight", PlanetFlightSetup.Apply, PlanetFlightSetup.IsInstalled),
            new Step("First Person", FirstPersonSetup.Apply, FirstPersonSetup.IsInstalled),
            new Step("Scene Flow", MothershipPhaseSetup.Apply, MothershipPhaseSetup.IsSector)
        };

        [MenuItem(MenuPath, priority = 199)]
        private static void Run()
        {
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRun()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>Runs every step in order and stops at the first that cannot run. Returns true when the sector is complete.</summary>
        public static bool Apply()
        {
            // Steps change the selection, and the ship is found through it, so each step starts from the user's selection.
            GameObject selected = Selection.activeGameObject;
            foreach (Step step in Steps)
            {
                Selection.activeGameObject = selected;
                if (!step.Apply())
                {
                    Debug.LogWarning($"Sector setup stopped at {step.Label}. Fix the problem in the dialog, then run it again.");
                    return false;
                }
            }

            Debug.Log($"'{SceneManager.GetActiveScene().name}' is set up as a sector.");
            return true;
        }

        /// <summary>Fills the list with the labels of the steps the scene is missing. Empty when the sector is complete.</summary>
        public static void FindMissing(Scene scene, List<string> missing)
        {
            missing.Clear();
            foreach (Step step in Steps)
            {
                if (!step.IsInstalled(scene))
                    missing.Add(step.Label);
            }
        }

        private static bool Has<T>(Scene scene) where T : Component
        {
            return SceneSetupUtility.FindInScene<T>(scene).Count > 0;
        }
    }
}
