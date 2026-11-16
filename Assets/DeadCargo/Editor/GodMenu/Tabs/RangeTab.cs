using DeadCargo.EditorTools.TestRange;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Keeps the test range blank and spawns its content into the open scene on demand.</summary>
    internal sealed class RangeTab : GodMenuTab
    {
        private const string DialogTitle = "Test Range";

        private static readonly string[] SceneLabels = { "Spawn Everything", "Clear Open Scene" };
        private static readonly string[] TemplateLabels = { "Save Open Scene As Template" };

        private readonly bool[] optionalSpawned = new bool[TestRangeLibrary.OptionalGroups.Length];
        private bool hasTemplate;
        private bool rigSpawned;
        private bool sceneEmpty;

        public override string Title => "Range";
        public override int Order => 5;

        public override void OnUpdate()
        {
            Scene scene = SceneManager.GetActiveScene();

            hasTemplate = TestRangeLibrary.HasTemplate;
            rigSpawned = TestRangeLibrary.IsSpawned(TestRangeLibrary.PlayerRig, scene);
            sceneEmpty = scene.rootCount == 0;

            for (int i = 0; i < optionalSpawned.Length; ++i)
                optionalSpawned[i] = TestRangeLibrary.IsSpawned(TestRangeLibrary.OptionalGroups[i], scene);
        }

        public override void OnGUI()
        {
            // Scene edits are not available while the game is running.
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                DrawSpawn();
                DrawTemplate();
            }
        }

        private void DrawSpawn()
        {
            using (var section = new GodMenuGUI.SectionScope("Spawn Into Open Scene"))
            {
                if (!section.Expanded)
                    return;

                if (!hasTemplate)
                {
                    EditorGUILayout.HelpBox("No template yet. Open the fully built test range and save it as the template below.", MessageType.Info);
                    return;
                }

                Scene scene = SceneManager.GetActiveScene();

                GodMenuGUI.BeginItemRow(TestRangeLibrary.PlayerRig.Label, rigSpawned ? "In scene" : null);
                bool spawnRig;
                using (new EditorGUI.DisabledScope(rigSpawned))
                    spawnRig = GodMenuGUI.MiniButton("Spawn", 60f);
                GodMenuGUI.EndItemRow();

                if (spawnRig)
                    RunSceneEdit(() => TestRangeLibrary.Spawn(TestRangeLibrary.PlayerRig, scene));

                for (int i = 0; i < TestRangeLibrary.OptionalGroups.Length; ++i)
                {
                    RangeGroup group = TestRangeLibrary.OptionalGroups[i];
                    bool spawned = optionalSpawned[i];

                    GodMenuGUI.BeginItemRow(group.Label, spawned ? "In scene" : null);
                    bool clicked = GodMenuGUI.MiniButton(spawned ? "Remove" : "Spawn", 60f);
                    GodMenuGUI.EndItemRow();

                    if (!clicked)
                        continue;

                    if (spawned)
                        RunSceneEdit(() => TestRangeLibrary.Remove(group, scene));
                    else
                        RunSceneEdit(() => TestRangeLibrary.Spawn(group, scene));
                }

                GUILayout.Space(4f);
                bool[] enabled = { sceneEmpty, !sceneEmpty };

                switch (GodMenuGUI.ButtonRow(SceneLabels, enabled))
                {
                    case 0:
                        RunSceneEdit(() => TestRangeLibrary.SpawnEverything(scene));
                        break;
                    case 1:
                        RunSceneEdit(() => ClearScene(scene));
                        break;
                }
            }
        }

        private void DrawTemplate()
        {
            using (var section = new GodMenuGUI.SectionScope("Template"))
            {
                if (!section.Expanded)
                    return;

                GodMenuGUI.Status(hasTemplate, hasTemplate ? "Template saved" : "No template saved");

                if (GodMenuGUI.ButtonRow(TemplateLabels) == 0)
                    RunSceneEdit(SaveTemplate);
            }
        }

        private void SaveTemplate()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!rigSpawned)
            {
                EditorUtility.DisplayDialog(DialogTitle,
                    "The open scene has no player rig, so it would make an empty template.\n\nOpen or spawn the full test range first.", "OK");
                return;
            }

            string warning = hasTemplate ? "This replaces the existing template.\n\n" : string.Empty;
            if (!EditorUtility.DisplayDialog(DialogTitle,
                    $"{warning}Save '{scene.name}' and store a copy of it as the test range template?", "Save Template", "Cancel"))
                return;

            if (!TestRangeLibrary.SaveTemplate(scene))
                EditorUtility.DisplayDialog(DialogTitle, "The template could not be saved. Save the scene to a file first.", "OK");
        }

        private static void ClearScene(Scene scene)
        {
            if (EditorUtility.DisplayDialog(DialogTitle,
                    $"Remove every object from '{scene.name}'?\n\nYou can undo this with Ctrl+Z until the scene is closed.",
                    "Clear Scene", "Cancel"))
                TestRangeLibrary.Clear(scene);
        }

        // Scene edits and dialogs change the layout mid-draw, so the current GUI pass is abandoned afterwards.
        private void RunSceneEdit(System.Action edit)
        {
            edit();
            OnUpdate();
            GUIUtility.ExitGUI();
        }
    }
}
