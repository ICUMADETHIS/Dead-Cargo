using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.Flow
{
    /// <summary>Moves the game between the mothership and sector scenes behind the loading screen.</summary>
    public static class SceneFlow
    {
        private const string MothershipTitle = "RETURNING TO MOTHERSHIP";
        private const string SectorTitle = "JUMPING TO SECTOR";

        private static SceneFlowSettings settings;

        /// <summary>The settings asset from Resources, or null when the mothership phase has not been set up.</summary>
        public static SceneFlowSettings Settings
        {
            get
            {
                if (settings == null)
                    settings = Resources.Load<SceneFlowSettings>(SceneFlowSettings.ResourceName);
                return settings;
            }
        }

        public static bool IsLoading => LoadingScreen.IsLoading;
        public static bool HasMothership => Settings != null && !string.IsNullOrEmpty(Settings.MothershipScene);
        public static bool HasSector => Settings != null && !string.IsNullOrEmpty(Settings.SectorScene);

        /// <summary>Display name of the sector scene, or an empty string when none is set.</summary>
        public static string SectorName => HasSector ? Path.GetFileNameWithoutExtension(Settings.SectorScene) : string.Empty;

        /// <summary>True while the mothership scene is the active scene.</summary>
        public static bool InMothership => HasMothership && SceneManager.GetActiveScene().path == Settings.MothershipScene;

        /// <summary>Loads the mothership. Returns false when it is not set up or a load is already running.</summary>
        public static bool LoadMothership()
        {
            return HasMothership && LoadScene(Settings.MothershipScene, MothershipTitle);
        }

        /// <summary>Loads the sector. Returns false when it is not set up or a load is already running.</summary>
        public static bool LaunchSector()
        {
            return HasSector && LoadScene(Settings.SectorScene, SectorTitle);
        }

        /// <summary>Loads a scene by project path behind the loading screen. Returns false if a load is already running.</summary>
        public static bool LoadScene(string scenePath, string title)
        {
            return LoadingScreen.Begin(scenePath, title);
        }
    }
}
