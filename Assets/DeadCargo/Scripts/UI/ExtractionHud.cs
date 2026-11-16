using System;
using DeadCargo.Cargo;
using DeadCargo.Extraction;
using DeadCargo.Heat;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.UI
{
    /// <summary>Shows run heat, the jump drive and the player's current ship hold using the live gameplay services.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public sealed class ExtractionHud : MonoBehaviour
    {
        private const KeyCode ManifestKey = KeyCode.I;
        private const float NoticeDuration = 2.7f;
        private const int UiLayer = 5;
        private const int SortingOrder = 10;

        private ExtractionHudView view;
        private GameAgentManager agents;
        private Vehicle vehicle;
        private CargoHold hold;
        private CargoJettison jettison;
        private CargoLoadHandling handling;
        private JumpDrive jumpDrive;
        private HeatTracker heat;
        private RunTracker run;
        private int displayedHeat = -1;
        private int displayedTier = -1;
        private int displayedJumpSeconds = -1;
        private int manifestPage;
        private bool manifestOpen;
        private float noticeStarted;
        private bool noticeVisible;
        private bool noticeUrgent;

        /// <summary>Installs a single HUD in a loaded scene. Use on a scene object to add it in the editor.</summary>
        public static void Install(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<ExtractionHud>(true) != null)
                    return;
            }

            var hud = new GameObject("Extraction HUD", typeof(RectTransform)) { layer = UiLayer };
            SceneManager.MoveGameObjectToScene(hud, scene);
            hud.AddComponent<ExtractionHud>();
        }

        private void Awake()
        {
            HudFactory.ConfigureCanvas(gameObject, SortingOrder);
            view = new ExtractionHudView((RectTransform)transform);
        }

        private void Start()
        {
            agents = GameAgentManager.Instance;
            if (agents != null)
            {
                agents.onFocusedVehicleChanged.AddListener(OnFocusedVehicleChanged);
                GameAgent player = agents.FocusedGameAgent;
                OnFocusedVehicleChanged(player != null ? player.Vehicle : null);
            }

            heat = HeatTracker.Active;
            if (heat != null)
            {
                view.SetHeatBoundaries(GetHeatBoundaries(heat));
                heat.Changed += RefreshHeat;
                RefreshHeat();
            }

            run = RunTracker.Active;
            if (run != null)
                run.Ended += OnRunEnded;

            Layout();
        }

        private void OnDestroy()
        {
            if (agents != null)
                agents.onFocusedVehicleChanged.RemoveListener(OnFocusedVehicleChanged);
            if (heat != null)
                heat.Changed -= RefreshHeat;
            if (run != null)
                run.Ended -= OnRunEnded;
            UnbindVehicle();
        }

        private void Update()
        {
            if (jumpDrive != null && jumpDrive.IsSpooling)
                UpdateSpool();

            if (hold != null && Time.timeScale > 0f)
            {
                if (Input.GetKeyDown(ManifestKey))
                    SetManifestOpen(!manifestOpen);

                if (manifestOpen)
                {
                    if (Input.GetKeyDown(KeyCode.PageUp))
                        SetManifestPage(manifestPage - 1);
                    else if (Input.GetKeyDown(KeyCode.PageDown))
                        SetManifestPage(manifestPage + 1);
                }
            }

            if (!noticeVisible)
                return;

            float elapsed = Time.unscaledTime - noticeStarted;
            if (elapsed >= NoticeDuration)
            {
                noticeVisible = false;
                Layout();
            }
            else
            {
                float fade = Mathf.Min(elapsed / .13f, (NoticeDuration - elapsed) / .25f);
                view.SetNoticeOpacity(Mathf.Clamp01(fade));
            }
        }

        private void OnFocusedVehicleChanged(Vehicle current)
        {
            GameAgent player = agents.FocusedGameAgent;
            if (player == null || !player.IsPlayer || current == null || current.Destroyed)
                current = null;

            if (current == vehicle)
                return;

            UnbindVehicle();
            vehicle = current;
            if (vehicle == null)
            {
                Layout();
                return;
            }

            vehicle.onDestroyed.AddListener(OnVehicleDestroyed);

            hold = vehicle.GetComponent<CargoHold>();
            if (hold != null)
            {
                jettison = vehicle.GetComponent<CargoJettison>();
                handling = vehicle.GetComponent<CargoLoadHandling>();
                hold.Changed += RefreshCargo;
                hold.Added += OnCargoAdded;
                hold.CapacityExceeded += OnCapacityExceeded;
                if (jettison != null)
                    jettison.Jettisoned += OnJettisoned;

                RefreshCargo();
            }

            jumpDrive = vehicle.GetComponent<JumpDrive>();
            if (jumpDrive != null)
            {
                jumpDrive.SpoolStarted += OnSpoolStarted;
                jumpDrive.SpoolCancelled += OnSpoolCancelled;
                RefreshJumpDrive();
            }

            Layout();
        }

        private void UnbindVehicle()
        {
            if (vehicle != null)
                vehicle.onDestroyed.RemoveListener(OnVehicleDestroyed);
            if (hold != null)
            {
                hold.Changed -= RefreshCargo;
                hold.Added -= OnCargoAdded;
                hold.CapacityExceeded -= OnCapacityExceeded;
            }
            if (jettison != null)
                jettison.Jettisoned -= OnJettisoned;
            if (jumpDrive != null)
            {
                jumpDrive.SpoolStarted -= OnSpoolStarted;
                jumpDrive.SpoolCancelled -= OnSpoolCancelled;
            }

            vehicle = null;
            hold = null;
            handling = null;
            jettison = null;
            jumpDrive = null;
            manifestOpen = false;
            manifestPage = 0;
            noticeVisible = false;
        }

        private void OnVehicleDestroyed()
        {
            UnbindVehicle();
            Layout();
        }

        private void OnRunEnded(RunResult result)
        {
            // The run result screen takes over.
            gameObject.SetActive(false);
        }

        private void RefreshHeat()
        {
            int score = Mathf.FloorToInt(heat.Heat);
            int index = heat.TierIndex;
            if (score == displayedHeat && index == displayedTier)
                return;

            displayedHeat = score;
            displayedTier = index;
            HeatTier currentTier = heat.CurrentTier;
            view.SetHeat(score, currentTier.Label, currentTier.Color, heat.NormalizedHeat);
        }

        private void RefreshCargo()
        {
            view.SetCargo(hold, manifestOpen);
            if (manifestOpen)
                SetManifestPage(manifestPage);

            // The idle spool time depends on the load.
            if (jumpDrive != null && !jumpDrive.IsSpooling)
                view.SetJumpReady(jumpDrive.EstimatedSpoolTime);
        }

        private void RefreshJumpDrive()
        {
            if (jumpDrive.IsSpooling)
            {
                displayedJumpSeconds = -1;
                UpdateSpool();
            }
            else
            {
                view.SetJumpReady(jumpDrive.EstimatedSpoolTime);
            }
        }

        // The countdown text only changes once a second, so it is rebuilt only then.
        private void UpdateSpool()
        {
            int seconds = Mathf.CeilToInt(jumpDrive.SpoolRemaining);
            if (seconds != displayedJumpSeconds)
            {
                displayedJumpSeconds = seconds;
                view.SetJumpCountdown(seconds);
            }

            view.SetJumpProgress(jumpDrive.SpoolProgress);
        }

        private void SetManifestOpen(bool open)
        {
            manifestOpen = open;
            RefreshCargo();
            Layout();
        }

        private void SetManifestPage(int page)
        {
            int last = Mathf.Max(0, (hold.Stacks.Count - 1) / ExtractionHudView.RowsPerPage);
            manifestPage = Mathf.Clamp(page, 0, last);
            view.SetManifest(hold, handling, jettison != null && jettison.CanJettison, manifestPage);
            Layout();
        }

        private void OnCargoAdded(CargoItemDefinition item, int count)
        {
            ShowNotice($"+{count}  {item.DisplayName}   ·   +{item.UnitMass * count:0.#} t", HudFactory.CargoColor, false);
        }

        private void OnCapacityExceeded(CargoItemDefinition item)
        {
            ShowNotice("HOLD FULL  ·  Cargo left in space", HudFactory.WarningColor, true);
        }

        private void OnJettisoned(CargoStack stack)
        {
            ShowNotice($"JETTISONED  ·  {stack.Mass:0.#} t released", HudFactory.CargoColor, false);
        }

        private void OnSpoolStarted()
        {
            RefreshJumpDrive();
            ShowNotice($"JUMP DRIVE SPOOLING  ·  Survive {Mathf.CeilToInt(jumpDrive.SpoolDuration)} s", HudFactory.JumpColor, true);
        }

        private void OnSpoolCancelled()
        {
            RefreshJumpDrive();
            ShowNotice("JUMP SPOOL CANCELLED", HudFactory.WarningColor, false);
        }

        private void ShowNotice(string message, Color color, bool urgent)
        {
            if (noticeVisible && noticeUrgent && !urgent && Time.unscaledTime - noticeStarted < .45f)
                return;

            noticeVisible = true;
            noticeUrgent = urgent;
            noticeStarted = Time.unscaledTime;
            view.SetNotice(message, color);
            view.SetNoticeOpacity(0f);
            Layout();
        }

        private void Layout()
        {
            view.Layout(heat != null, hold != null, jumpDrive != null, manifestOpen, noticeVisible);
        }

        private static float[] GetHeatBoundaries(HeatTracker tracker)
        {
            var boundaries = new float[tracker.Tiers.Count + 2];
            int count = 1;
            for (int i = 0; i < tracker.Tiers.Count; i++)
            {
                float threshold = tracker.Tiers[i].Threshold / tracker.MaxHeat;
                if (threshold > boundaries[count - 1] && threshold < 1f)
                    boundaries[count++] = threshold;
            }
            boundaries[count++] = 1f;
            Array.Resize(ref boundaries, count);
            return boundaries;
        }
    }
}
