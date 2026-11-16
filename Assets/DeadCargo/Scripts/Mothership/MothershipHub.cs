using DeadCargo.Extraction;
using DeadCargo.Flow;
using DeadCargo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.Mothership
{
    /// <summary>
    /// The mothership hub screen. Shows the last run and launches the next one.
    /// Phase 6b adds the stash, credits and selling.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
    public sealed class MothershipHub : MonoBehaviour
    {
        private const KeyCode LaunchKey = KeyCode.Return;
        private const KeyCode LaunchKeypadKey = KeyCode.KeypadEnter;
        private const int SortingOrder = 10;

        private MothershipHubView view;

        /// <summary>Launches the next run into the sector. Returns false when no sector is set up or a load is running.</summary>
        public bool Launch()
        {
            return SceneFlow.LaunchSector();
        }

        private void Awake()
        {
            HudFactory.ConfigureCanvas(gameObject, SortingOrder);
            view = new MothershipHubView((RectTransform)transform, () => Launch());
        }

        private void Start()
        {
            // A sector run may have left the world paused and the cursor locked.
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            view.ShowLastRun(RunTracker.LastResult);
            view.SetLaunch(SceneFlow.SectorName, SceneFlow.HasSector);
        }

        private void Update()
        {
            if (!SceneFlow.IsLoading && (Input.GetKeyDown(LaunchKey) || Input.GetKeyDown(LaunchKeypadKey)))
                Launch();
        }
    }
}
