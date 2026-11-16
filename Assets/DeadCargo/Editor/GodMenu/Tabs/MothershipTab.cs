using System.IO;
using DeadCargo.Cargo;
using DeadCargo.Flow;
using DeadCargo.Profile;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeadCargo.EditorTools.GodMenu
{
    /// <summary>
    /// Scene flow between the mothership and the sector, and the saved profile.
    /// The profile is a file, so its controls work outside Play Mode too.
    /// </summary>
    internal sealed class MothershipTab : GodMenuTab
    {
        private const int DebugCredits = 1000;

        private static readonly string[] PlayLabels = { "Go To Mothership", "Launch Sector" };
        private static readonly string[] EditLabels = { "Open Mothership", "Open Sector" };
        private static readonly string[] SettingsLabels = { "Select Settings" };
        private static readonly string[] ProfileLabels = { "+1,000 cr", "Sell All", "Clear Stash" };
        private static readonly string[] ProfileFileLabels = { "Reset Profile", "Open Save Folder" };

        public override string Title => "Mothership";
        public override int Order => 45;

        public override void OnGUI()
        {
            SceneFlowSettings settings = SceneFlow.Settings;
            if (settings == null)
                EditorGUILayout.HelpBox("No scene flow yet. Run the Phase 6 setup from the Project tab with the sector scene open.", MessageType.Warning);
            else
                DrawSceneFlow(settings);

            DrawProfile();
        }

        private static void DrawProfile()
        {
            using (var section = new GodMenuGUI.SectionScope("Profile"))
            {
                if (!section.Expanded)
                    return;

                PlayerProfile profile = ProfileStore.Current;
                CargoCatalog catalog = CargoCatalog.Instance;
                bool hasStash = profile.Stash.Count > 0;

                GodMenuGUI.Field("Credits", $"{profile.Credits:N0} cr");
                GodMenuGUI.Field("Stash", $"{profile.Stash.Count} stack(s)  ·  {StashValue(profile, catalog):N0} cr");
                GodMenuGUI.Field("Record", $"{profile.RunsExtracted} extracted  ·  {profile.RunsLost} lost");

                bool[] enabled = { true, hasStash && catalog != null, hasStash };
                switch (GodMenuGUI.ButtonRow(ProfileLabels, enabled))
                {
                    case 0:
                        ProfileStore.AddCredits(DebugCredits);
                        break;
                    case 1:
                        ProfileStore.SellAll(catalog);
                        break;
                    case 2:
                        ProfileStore.ClearStash();
                        break;
                }

                switch (GodMenuGUI.ButtonRow(ProfileFileLabels))
                {
                    case 0:
                        if (EditorUtility.DisplayDialog("Reset Profile",
                                "Erase all credits, the stash, the service record and the last run?\n\nThis cannot be undone.", "Reset", "Cancel"))
                            ProfileStore.ResetProfile();
                        GUIUtility.ExitGUI();
                        break;
                    case 1:
                        EditorUtility.RevealInFinder(File.Exists(ProfileStore.FilePath) ? ProfileStore.FilePath : Application.persistentDataPath);
                        break;
                }
            }
        }

        private static int StashValue(PlayerProfile profile, CargoCatalog catalog)
        {
            if (catalog == null)
                return 0;

            int value = 0;
            foreach (StashEntry entry in profile.Stash)
            {
                CargoItemDefinition item = catalog.Find(entry.ItemId);
                if (item != null)
                    value += item.UnitValue * entry.Quantity;
            }

            return value;
        }

        private static void DrawSceneFlow(SceneFlowSettings settings)
        {
            using (var section = new GodMenuGUI.SectionScope("Scene Flow"))
            {
                if (!section.Expanded)
                    return;

                GodMenuGUI.Field("Mothership", SceneLabel(settings.MothershipScene));
                GodMenuGUI.Field("Sector", SceneLabel(settings.SectorScene));

                if (GodMenuContext.IsPlaying)
                    DrawTravel();
                else
                    DrawOpen(settings);

                if (GodMenuGUI.ButtonRow(SettingsLabels) == 0)
                {
                    Selection.activeObject = settings;
                    EditorGUIUtility.PingObject(settings);
                }
            }
        }

        private static void DrawTravel()
        {
            bool idle = !SceneFlow.IsLoading;
            GodMenuGUI.Field("Loading", idle ? "No" : "Yes");

            bool[] enabled = { idle && SceneFlow.HasMothership && !SceneFlow.InMothership, idle && SceneFlow.HasSector };
            switch (GodMenuGUI.ButtonRow(PlayLabels, enabled))
            {
                case 0:
                    SceneFlow.LoadMothership();
                    break;
                case 1:
                    SceneFlow.LaunchSector();
                    break;
            }
        }

        private static void DrawOpen(SceneFlowSettings settings)
        {
            bool[] enabled = { SceneExists(settings.MothershipScene), SceneExists(settings.SectorScene) };
            switch (GodMenuGUI.ButtonRow(EditLabels, enabled))
            {
                case 0:
                    OpenScene(settings.MothershipScene);
                    break;
                case 1:
                    OpenScene(settings.SectorScene);
                    break;
            }
        }

        // Opening a scene rebuilds the editor layout mid-draw, so the current GUI pass is abandoned.
        private static void OpenScene(string path)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path);

            GUIUtility.ExitGUI();
        }

        private static bool SceneExists(string path)
        {
            return !string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
        }

        private static string SceneLabel(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "Not set";

            return SceneExists(path) ? Path.GetFileNameWithoutExtension(path) : "Missing";
        }
    }
}
