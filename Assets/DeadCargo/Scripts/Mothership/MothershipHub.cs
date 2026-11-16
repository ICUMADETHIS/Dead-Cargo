using System.Collections.Generic;
using DeadCargo.Cargo;
using DeadCargo.Flow;
using DeadCargo.Profile;
using DeadCargo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DeadCargo.Mothership
{
    /// <summary>The mothership hub screen: credits, the last run, the service record, the stash with selling, and launch.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
    public sealed class MothershipHub : MonoBehaviour
    {
        private const KeyCode LaunchKey = KeyCode.Return;
        private const KeyCode LaunchKeypadKey = KeyCode.KeypadEnter;
        private const KeyCode SellAllKey = KeyCode.S;
        private const int SortingOrder = 10;

        // The stash as shown, most valuable first. Row buttons index into it.
        private readonly List<CargoStack> stash = new List<CargoStack>();
        private MothershipHubView view;
        private CargoCatalog catalog;

        /// <summary>Launches the next run into the sector. Returns false when no sector is set up or a load is running.</summary>
        public bool Launch()
        {
            return SceneFlow.LaunchSector();
        }

        /// <summary>Sells everything in the stash. Returns the credits earned.</summary>
        public int SellAll()
        {
            return ProfileStore.SellAll(catalog);
        }

        private void Awake()
        {
            HudFactory.ConfigureCanvas(gameObject, SortingOrder);
            view = new MothershipHubView((RectTransform)transform, () => Launch(), SellRow, () => SellAll());
            catalog = CargoCatalog.Instance;
            if (catalog == null)
                Debug.LogWarning("No cargo catalog in Resources, so the stash cannot be shown. Run the Phase 6 setup again.", this);
        }

        private void OnEnable()
        {
            ProfileStore.Changed += Refresh;
        }

        private void OnDisable()
        {
            ProfileStore.Changed -= Refresh;
        }

        private void Start()
        {
            // A sector run may have left the world paused and the cursor locked.
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            view.SetLaunch(SceneFlow.SectorName, SceneFlow.HasSector);
            Refresh();
        }

        private void Update()
        {
            if (SceneFlow.IsLoading)
                return;

            if (Input.GetKeyDown(LaunchKey) || Input.GetKeyDown(LaunchKeypadKey))
                Launch();
            else if (Input.GetKeyDown(SellAllKey))
                SellAll();
        }

        private void Refresh()
        {
            PlayerProfile profile = ProfileStore.Current;
            view.SetCredits(profile.Credits);
            view.SetRecord(profile.RunsExtracted, profile.RunsLost);
            view.ShowLastRun(profile.HasLastRun, profile.LastRun);

            ResolveStash(profile);
            view.SetStash(stash);
        }

        // Items the catalog does not know stay saved but are not shown.
        private void ResolveStash(PlayerProfile profile)
        {
            stash.Clear();
            if (catalog == null)
                return;

            foreach (StashEntry entry in profile.Stash)
            {
                CargoItemDefinition item = catalog.Find(entry.ItemId);
                if (item != null)
                    stash.Add(new CargoStack(item, entry.Quantity));
            }

            stash.Sort((a, b) => b.Value.CompareTo(a.Value));
        }

        private void SellRow(int row)
        {
            if (row >= 0 && row < stash.Count)
                ProfileStore.Sell(stash[row].Item);
        }
    }
}
