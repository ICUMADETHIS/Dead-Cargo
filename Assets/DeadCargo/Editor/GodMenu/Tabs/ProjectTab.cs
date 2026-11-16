using System.IO;
using DeadCargo.Authority;
using DeadCargo.Cargo;
using DeadCargo.Extraction;
using DeadCargo.Heat;
using UnityEditor;
using UnityEngine;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Phase setup for the open scene, and shortcuts to the project docs.</summary>
    internal sealed class ProjectTab : GodMenuTab
    {
        /// <summary>One phase's setup tool and whether the open scene already has it.</summary>
        private sealed class Phase
        {
            public Phase(string label, string menuPath, System.Func<bool> isInstalled)
            {
                Label = label;
                MenuPath = menuPath;
                IsInstalled = isInstalled;
            }

            public string Label { get; }
            public string MenuPath { get; }
            public System.Func<bool> IsInstalled { get; }
            public bool Installed { get; set; }
        }

        // Add a line here when a new phase ships a setup tool.
        private readonly Phase[] phases =
        {
            new Phase("Phase 1 - Cargo", "Dead Cargo/Setup/Phase 1 - Cargo (Open Scene)", Exists<CargoHold>),
            new Phase("Phase 2 - Jettison", "Dead Cargo/Setup/Phase 2 - Jettison (Open Scene)", Exists<CargoJettison>),
            new Phase("Phase 3 - Heat", "Dead Cargo/Setup/Phase 3 - Heat (Open Scene)", Exists<HeatTracker>),
            new Phase("Phase 4 - Authority", "Dead Cargo/Setup/Phase 4 - Authority (Open Scene)", Exists<AuthorityResponse>),
            new Phase("Phase 5 - Extraction", "Dead Cargo/Setup/Phase 5 - Extraction (Open Scene)", Exists<RunTracker>),
            new Phase("Phase 6 - Mothership", MothershipPhaseSetup.MenuPath, () => MothershipPhaseSetup.IsInstalled)
        };

        private static readonly string[] HierarchyLabels = { "Tidy Open Scene" };

        // Matching entries: button label and project-relative path.
        private static readonly string[] DocLabels = { "Game Design", "Roadmap", "Code Standards", "Backlog" };
        private static readonly string[] DocPaths =
        {
            "Docs/GameDesign.md", "Docs/Roadmap.md", "Docs/CodeStandards.md", "Docs/Backlog.md"
        };

        public override string Title => "Project";
        public override int Order => 1000;

        public override void OnUpdate()
        {
            foreach (Phase phase in phases)
                phase.Installed = phase.IsInstalled();
        }

        public override void OnGUI()
        {
            DrawPhases();
            DrawHierarchy();
            DrawDocs();
        }

        private static void DrawHierarchy()
        {
            using (var section = new GodMenuGUI.SectionScope("Hierarchy"))
            {
                if (!section.Expanded)
                    return;

                GUILayout.Label("Files Dead Cargo objects into the Systems, UI, Cameras and Environment folders. " +
                    "Kit objects and ships stay at the root.", EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GodMenuGUI.ButtonRow(HierarchyLabels) == 0)
                        EditorApplication.ExecuteMenuItem(SceneHierarchy.TidyMenuPath);
                }
            }
        }

        private void DrawPhases()
        {
            using (var section = new GodMenuGUI.SectionScope("Open Scene Setup"))
            {
                if (!section.Expanded)
                    return;

                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    foreach (Phase phase in phases)
                    {
                        EditorGUILayout.BeginHorizontal();
                        GodMenuGUI.Status(phase.Installed, phase.Label);

                        if (GodMenuGUI.MiniButton(phase.Installed ? "Re-run" : "Set Up", 60f))
                            EditorApplication.ExecuteMenuItem(phase.MenuPath);

                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }

        private static void DrawDocs()
        {
            using (var section = new GodMenuGUI.SectionScope("Docs"))
            {
                if (!section.Expanded)
                    return;

                int clicked = GodMenuGUI.ButtonRow(DocLabels);
                if (clicked >= 0 && File.Exists(DocPaths[clicked]))
                    EditorUtility.OpenWithDefaultApp(Path.GetFullPath(DocPaths[clicked]));
            }
        }

        private static bool Exists<T>() where T : Object
        {
            return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include) != null;
        }
    }
}
