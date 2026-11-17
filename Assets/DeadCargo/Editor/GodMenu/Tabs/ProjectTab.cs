using System.Collections.Generic;
using System.IO;
using DeadCargo.EditorTools.Visuals;
using DeadCargo.Flow;
using DeadCargo.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>Sets up and tidies the open scene, and opens the project docs.</summary>
    internal sealed class ProjectTab : GodMenuTab
    {
        private const string TidyLabel = "Tidy Hierarchy";
        private const string SaveLabel = "Save Scene";

        private static readonly string[] MothershipLabels = { SaveLabel, TidyLabel };
        private const string RigLabel = "Spawn Player In Ship";

        private static readonly string[] MissingSectorLabels = { SaveLabel, "Set Up Sector", TidyLabel };
        private static readonly string[] NoRigLabels = { SaveLabel, RigLabel, TidyLabel };
        private static readonly string[] ReadySectorLabels = { SaveLabel, "Re-run Setup", TidyLabel };

        private const string ApplySkyLabel = "Apply Space Sky";
        private const string BakeNebulaLabel = "Bake Nebula";
        private const string EditSkyLabel = "Edit Sky";
        private const string AddDustLabel = "Add Space Dust";
        private const string EditDustLabel = "Edit Dust";
        private const string AddFadeLabel = "Add Distance Fade";
        private const string EditFadeLabel = "Edit Fade";
        private const string ComicShipLabel = "Comic Ship";
        private const string EditShipLabel = "Edit Ship Look";
        private const string PlaceRockLabel = "Place Test Rock";
        private const string RemoveRockLabel = "Remove Test Rock";
        private const string SetUpSunLabel = "Set Up Sun";
        private const string SelectSunLabel = "Select Sun";

        // Matching entries: button label and project-relative path.
        private static readonly string[] DocLabels = { "Game Design", "Roadmap", "Code Standards", "Backlog" };
        private static readonly string[] DocPaths =
        {
            "Docs/GameDesign.md", "Docs/Roadmap.md", "Docs/CodeStandards.md", "Docs/Backlog.md"
        };

        private readonly List<string> missing = new List<string>();
        private bool isMothership;
        private bool isEmpty;
        private bool isGameSector;
        private bool needsRig;
        private bool hasSkyMaterial;
        private bool hasSpaceSky;
        private bool skyIsCurrent;
        private SpaceDust dust;
        private SpaceDepth depth;
        private bool shipIsComic;
        private GameObject testRock;
        private Light sun;
        private bool sunReady;

        // The Visuals section's buttons, one each for the sky, the dust, the distance fade, the ship, the test rock and
        // the sun, chosen in OnUpdate.
        private readonly string[] visualLabels = new string[6];

        public override string Title => "Project";
        public override GodMenuGroup Group => GodMenuGroup.Editor;
        public override int Order => 10;

        // Setup tools edit the scene, which is not possible in Play Mode.
        public override bool IsAvailable => !EditorApplication.isPlayingOrWillChangePlaymode;

        public override string Summary
        {
            get
            {
                if (isMothership)
                    return "Mothership";
                if (isEmpty)
                    return "Blank scene";

                return missing.Count == 0 ? "Sector ready" : $"{SectorSetup.Steps.Length - missing.Count} / {SectorSetup.Steps.Length} set up";
            }
        }

        public override void OnUpdate()
        {
            // Hidden in Play Mode, so the scene searches are skipped.
            if (EditorApplication.isPlaying)
                return;

            Scene scene = SceneManager.GetActiveScene();
            SceneFlowSettings settings = SceneFlow.Settings;
            isMothership = settings != null && !string.IsNullOrEmpty(scene.path) && scene.path == settings.MothershipScene;
            isEmpty = scene.rootCount == 0;
            SectorSetup.FindMissing(scene, missing);

            // Game sectors get their player from the rig setup. The test range and unsaved scenes use the Range tab.
            isGameSector = !isMothership && !string.IsNullOrEmpty(scene.path) && !SectorRigSetup.IsTestRange(scene);
            bool hasPlayer = SceneSetupUtility.FindPlayer(scene) != null;
            needsRig = isGameSector && !hasPlayer;
            hasSkyMaterial = SpaceSkySetup.HasSkyMaterial(scene);
            hasSpaceSky = SpaceSkySetup.IsInstalled(scene);
            skyIsCurrent = hasSkyMaterial && SpaceSkyBaker.IsCurrent(RenderSettings.skybox);
            dust = SpaceDustSetup.Find(scene);
            depth = SpaceDepthSetup.Find(scene);
            shipIsComic = hasPlayer && ComicLookSetup.IsShipComic(scene);
            testRock = ComicLookSetup.FindTestRock(scene);
            sun = SunSetup.Find(scene);
            sunReady = SunSetup.IsInstalled(scene);
            visualLabels[0] = !hasSpaceSky ? ApplySkyLabel : skyIsCurrent ? EditSkyLabel : BakeNebulaLabel;
            visualLabels[1] = dust == null ? AddDustLabel : EditDustLabel;
            visualLabels[2] = depth == null ? AddFadeLabel : EditFadeLabel;
            visualLabels[3] = shipIsComic ? EditShipLabel : ComicShipLabel;
            visualLabels[4] = testRock == null ? PlaceRockLabel : RemoveRockLabel;
            visualLabels[5] = sunReady ? SelectSunLabel : SetUpSunLabel;
        }

        public override void OnGUI()
        {
            DrawOpenScene();
            if (isGameSector)
                DrawVisuals();
            DrawDocs();
        }

        private void DrawOpenScene()
        {
            using (var section = new GodMenuGUI.SectionScope("Open Scene"))
            {
                if (!section.Expanded)
                    return;

                string[] labels;
                if (isMothership)
                {
                    GodMenuGUI.Status(true, "Mothership: no setup needed");
                    labels = MothershipLabels;
                }
                else if (needsRig)
                {
                    GodMenuGUI.Status(false, "Game sector: no player yet");
                    GUILayout.Label("Spawns the player rig with the player already aboard the ship, and makes this the sector the mothership launches into.",
                        EditorStyles.wordWrappedMiniLabel);
                    labels = NoRigLabels;
                }
                else if (missing.Count == 0)
                {
                    GodMenuGUI.Status(true, "Sector ready: every system is set up");
                    labels = ReadySectorLabels;
                }
                else
                {
                    GodMenuGUI.Status(false, isEmpty ? "Blank scene" : "Not a complete sector");
                    GUILayout.Label(isEmpty
                        ? "Spawn the player rig from the Range tab, then set it up as a sector."
                        : "Missing: " + string.Join(", ", missing), EditorStyles.wordWrappedMiniLabel);
                    labels = MissingSectorLabels;
                }

                int clicked = GodMenuGUI.ButtonRow(labels);
                if (clicked < 0)
                    return;

                // Run after this GUI pass, because setup can open dialogs and rebuild the hierarchy.
                // An unsaved scene asks where to save it.
                if (labels[clicked] == SaveLabel)
                    EditorApplication.delayCall += () => EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                else if (labels[clicked] == TidyLabel)
                    EditorApplication.delayCall += () => EditorApplication.ExecuteMenuItem(SceneHierarchy.TidyMenuPath);
                else if (labels[clicked] == RigLabel)
                    EditorApplication.delayCall += () => SectorRigSetup.Apply();
                else
                    EditorApplication.delayCall += () => SectorSetup.Apply();
            }
        }

        // The sector's sky, space dust and the comic look of near objects.
        private void DrawVisuals()
        {
            using (var section = new GodMenuGUI.SectionScope("Visuals"))
            {
                if (!section.Expanded)
                    return;

                // The flags are refreshed ten times a second, so the skybox, dust and rock are checked again here.
                Material sky = RenderSettings.skybox;
                if (!hasSkyMaterial || sky == null)
                    GodMenuGUI.Status(false, "Sky: not the space sky");
                else if (!hasSpaceSky)
                    GodMenuGUI.Status(false, "Sky: needs the new-each-load update");
                else
                    GodMenuGUI.Status(skyIsCurrent, skyIsCurrent ? "Sky: " + sky.name : "Sky: nebula out of date");

                if (sun == null)
                    GodMenuGUI.Status(false, "Sun: no directional light");
                else
                    GodMenuGUI.Status(sunReady, sunReady ? $"Sun: {sun.name} (turn it to move the sun)" : $"Sun: {sun.name} needs setting up");

                bool hasDust = dust != null;
                GodMenuGUI.Status(hasDust, hasDust ? "Space dust: on" : "Space dust: none");
                bool hasDepth = depth != null;
                GodMenuGUI.Status(hasDepth, hasDepth ? "Distance fade: on" : "Distance fade: none");
                GodMenuGUI.Status(shipIsComic, shipIsComic ? "Ship: comic look" : "Ship: Flat Kit look");

                int clicked = GodMenuGUI.ButtonRow(visualLabels);
                if (clicked < 0)
                    return;

                // Setup runs after this GUI pass, because it can open dialogs and change the hierarchy.
                Scene scene = SceneManager.GetActiveScene();
                switch (visualLabels[clicked])
                {
                    case ComicShipLabel:
                        EditorApplication.delayCall += () => ComicLookSetup.ApplyToShip();
                        break;
                    case EditShipLabel:
                        Material shipLook = ComicLookSetup.FindShipComicMaterial(scene);
                        if (shipLook != null)
                            SelectAndPing(shipLook);
                        break;
                    case PlaceRockLabel:
                        EditorApplication.delayCall += () => ComicLookSetup.PlaceTestRock();
                        break;
                    case RemoveRockLabel when testRock != null:
                        GameObject rock = testRock;
                        EditorApplication.delayCall += () =>
                        {
                            if (rock != null)
                                Undo.DestroyObjectImmediate(rock);
                        };
                        break;
                    case ApplySkyLabel:
                        EditorApplication.delayCall += () => SpaceSkySetup.Apply();
                        break;
                    case BakeNebulaLabel when sky != null:
                        EditorApplication.delayCall += () => SpaceSkyBaker.Bake(sky);
                        break;
                    case EditSkyLabel when sky != null:
                        SelectAndPing(sky);
                        break;
                    case AddDustLabel:
                        EditorApplication.delayCall += () => SpaceDustSetup.Apply();
                        break;
                    case EditDustLabel when hasDust:
                        SelectAndPing(dust.gameObject);
                        break;
                    case AddFadeLabel:
                        EditorApplication.delayCall += () => SpaceDepthSetup.Apply();
                        break;
                    case EditFadeLabel when hasDepth:
                        SelectAndPing(depth.gameObject);
                        break;
                    case SetUpSunLabel:
                        EditorApplication.delayCall += () => SunSetup.Apply();
                        break;
                    case SelectSunLabel when sun != null:
                        SelectAndPing(sun.gameObject);
                        break;
                }
            }
        }

        private static void SelectAndPing(Object target)
        {
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
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
    }
}
