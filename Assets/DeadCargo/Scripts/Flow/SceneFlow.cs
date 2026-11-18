using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeadCargo.Flow
{
    /// <summary>
    /// Moves the game between the mothership and sector scenes behind the loading screen. A new scene starts up hidden
    /// (<see cref="IsEntering"/>), with the player's controls held, and is shown once its setup is done
    /// (<see cref="Entered"/>). Anything that must not start in the dark, such as the run's clock, waits for that.
    /// </summary>
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

        /// <summary>Raised when the loading screen starts to show a new scene. Not raised for the scene the game starts in.</summary>
        public static event Action Entered;

        public static bool IsLoading => LoadingScreen.IsLoading;

        /// <summary>
        /// True while a new scene runs hidden behind the loading screen, from its switch until it is shown. Objects that
        /// start then and must not run in the dark wait for <see cref="Entered"/>.
        /// </summary>
        public static bool IsEntering => LoadGate.IsOpen;

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

        internal static void RaiseEntered()
        {
            Entered?.Invoke();
        }

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Entered = null;
        }
    }
}
