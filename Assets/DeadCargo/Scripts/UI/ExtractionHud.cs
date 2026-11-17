using System;
using System.Collections.Generic;
using DeadCargo.Authority;
using DeadCargo.Cargo;
using DeadCargo.Extraction;
using DeadCargo.Flight;
using DeadCargo.Heat;
using DeadCargo.Sectors;
using DeadCargo.Sectors.Far;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.UI
{
    /// <summary>
    /// The run HUD: heat and hunters, the jump drive, the extraction point, the player's hold, and screen markers
    /// for the extraction point, Authority ships and nearby pickups.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public sealed class ExtractionHud : MonoBehaviour
    {
        private const KeyCode ManifestKey = KeyCode.I;
        private const float NoticeDuration = 2.7f;
        private const float DisruptedDisplayTime = 0.6f;
        private const int UiLayer = 5;
        private const int SortingOrder = 10;

        // Jump plate states while spooling. Disrupted outranks held, because a hit is the more urgent news.
        private const int SpoolingState = 0;
        private const int HeldState = 1;
        private const int DisruptedState = 2;

        // Extraction point plate states.
        private const int PointDormant = 0;
        private const int PointInRange = 1;
        private const int PointUplink = 2;
        private const int PointOpen = 3;

        // Distances are shown in steps, so the text is not rebuilt every frame of flight. In metres.
        private const int DistanceStep = 10;

        // Pickups beyond this are not marked, in metres. Markers fade towards it, in steps so meshes rarely rebuild.
        private const float PickupMarkerRange = 800f;
        private const float PickupFadeSteps = 10f;
        private const int MarkerCapacity = 48;

        private const string PointMarkerDormant = "EXTRACTION";
        private const string PointMarkerUplink = "UPLINK";
        private const string PointMarkerOpen = "OPEN";
        private const string HunterMarker = "AUTHORITY";

        private static readonly HudMarkerStyle PointMarkerStyle = new HudMarkerStyle(26f, true);
        private static readonly HudMarkerStyle HunterMarkerStyle = new HudMarkerStyle(20f, true);
        private static readonly HudMarkerStyle PickupMarkerStyle = new HudMarkerStyle(11f, false);
        private static readonly HudMarkerStyle PlanetMarkerStyle = new HudMarkerStyle(18f, true, true);
        private static readonly HudMarkerStyle HomeMarkerStyle = new HudMarkerStyle(22f, true, true);

        private const int CruiseReady = 0;
        private const int CruiseAltitude = 1;
        private const int CruiseCharging = 2;
        private const int CruiseCruising = 3;
        private const double MetresPerKilometre = 1000.0;
        private const double AltitudeStep = 10.0;

        private HudMarkerLayer markers;
        private ExtractionHudView view;
        private GameAgentManager agents;
        private Vehicle vehicle;
        private CargoHold hold;
        private CargoJettison jettison;
        private CargoLoadHandling handling;
        private JumpDrive jumpDrive;
        private CruiseDrive cruise;
        private int displayedCruiseState = -1;
        private int displayedCruiseFigure = int.MinValue;
        private string[] planetLabels = Array.Empty<string>();
        private int labelledBuild = -1;
        private string homeLabel;
        private HeatTracker heat;
        private RunTracker run;
        private AuthorityResponse authority;
        private int displayedHunters = -1;
        private int displayedHeat = -1;
        private int displayedTier = -1;
        private int displayedJumpSeconds = -1;
        private int displayedJumpState = -1;
        private bool heldNoticeShown;
        private bool hasPointPlate;
        private int displayedPointState = -1;
        private int displayedPointValue = -1;
        private int displayedPointDistance = -1;
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

            // Built first, so the markers draw behind the plates.
            markers = new HudMarkerLayer((RectTransform)transform, MarkerCapacity);
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

            authority = AuthorityResponse.Active;
            SectorSpace space = SectorSpace.Active;
            homeLabel = space != null && space.Definition != null ? space.Definition.DisplayName.ToUpperInvariant() : "HOME";
            ExtractionPoint.StateChanged += OnPointStateChanged;
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
            ExtractionPoint.StateChanged -= OnPointStateChanged;
            UnbindVehicle();
        }

        private void Update()
        {
            if (jumpDrive != null && jumpDrive.IsSpooling)
                UpdateSpool();

            if (cruise != null)
                UpdateCruise();

            UpdatePoint();
            UpdateHunters();
            UpdateMarkers();

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
                jumpDrive.HeldChanged += OnSpoolHeldChanged;
                jumpDrive.FuelShortage += OnFuelShortage;
                RefreshJumpDrive();
            }

            cruise = vehicle.GetComponent<CruiseDrive>();
            if (cruise != null)
            {
                cruise.ChargeCancelled += OnCruiseCancelled;
                cruise.Dropped += OnCruiseDropped;
                displayedCruiseState = -1;
                UpdateCruise();
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
                jumpDrive.HeldChanged -= OnSpoolHeldChanged;
                jumpDrive.FuelShortage -= OnFuelShortage;
            }
            if (cruise != null)
            {
                cruise.ChargeCancelled -= OnCruiseCancelled;
                cruise.Dropped -= OnCruiseDropped;
            }

            vehicle = null;
            cruise = null;
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

            // The idle plate shows fuel or spool time, and both depend on the hold.
            if (jumpDrive != null && !jumpDrive.IsSpooling)
                ShowIdleJumpDrive();
        }

        private void RefreshJumpDrive()
        {
            if (jumpDrive.IsSpooling)
            {
                displayedJumpSeconds = -1;
                displayedJumpState = -1;
                UpdateSpool();
            }
            else
            {
                ShowIdleJumpDrive();
            }
        }

        private void ShowIdleJumpDrive()
        {
            if (jumpDrive.HasFuel)
                view.SetJumpReady(jumpDrive.EstimatedSpoolTime);
            else
                view.SetJumpFuel(jumpDrive.DarkMatterAboard, jumpDrive.DarkMatterPerJump);
        }

        // The plate text only changes when the whole second or the state changes, so it is rebuilt only then.
        private void UpdateSpool()
        {
            int state = jumpDrive.TimeSinceKnockback < DisruptedDisplayTime ? DisruptedState
                : jumpDrive.IsHeld ? HeldState
                : SpoolingState;
            Color color = state == SpoolingState ? HudFactory.JumpColor : HudFactory.WarningColor;

            int seconds = Mathf.CeilToInt(jumpDrive.SpoolRemaining);
            if (seconds != displayedJumpSeconds || state != displayedJumpState)
            {
                displayedJumpSeconds = seconds;
                displayedJumpState = state;
                view.SetJumpCountdown(seconds, SpoolStatus(state), color);
            }

            view.SetJumpProgress(jumpDrive.SpoolProgress, color);
        }

        private void UpdateHunters()
        {
            int hunters = authority != null ? authority.ActiveShipCount : 0;
            if (hunters == displayedHunters)
                return;

            displayedHunters = hunters;
            view.SetHunters(hunters);
        }

        // Markers show only while the player flies a ship with a hold. Indexed loops, so no enumerators are allocated.
        private void UpdateMarkers()
        {
            if (hold == null || !markers.Begin(Camera.main))
            {
                markers.Hide();
                return;
            }

            Vector3 ship = vehicle.transform.position;

            IReadOnlyList<ExtractionPoint> points = ExtractionPoint.All;
            for (int i = 0; i < points.Count; ++i)
            {
                ExtractionPoint point = points[i];
                markers.Add(point.transform.position, PointMarkerStyle, PointColor(point.State), PointMarkerLabel(point.State),
                    RoundDistance(point.DistanceTo(ship)));
            }

            if (authority != null)
            {
                IReadOnlyList<Vehicle> hunters = authority.ActiveShips;
                for (int i = 0; i < hunters.Count; ++i)
                {
                    // Ships asleep with the sector, while the player is away in cruise, are not marked.
                    Vehicle hunter = hunters[i];
                    if (hunter != null && !hunter.Destroyed && hunter.gameObject.activeInHierarchy)
                        markers.Add(hunter.transform.position, HunterMarkerStyle, HudFactory.WarningColor, HunterMarker,
                            RoundDistance(Vector3.Distance(hunter.transform.position, ship)));
                }
            }

            IReadOnlyList<CargoPickup> pickups = CargoPickup.All;
            for (int i = 0; i < pickups.Count; ++i)
            {
                CargoPickup pickup = pickups[i];
                if (pickup.Item == null)
                    continue;

                float distance = Vector3.Distance(pickup.transform.position, ship);
                if (distance > PickupMarkerRange)
                    continue;

                float fade = Mathf.Round((1f - 0.6f * distance / PickupMarkerRange) * PickupFadeSteps) / PickupFadeSteps;
                Color color = pickup.Item.Slot == CargoSlot.DarkMatter ? HudFactory.DarkMatterColor : HudFactory.CargoColor;
                markers.Add(pickup.transform.position, PickupMarkerStyle, color, null, 0, fade);
            }

            AddPlaceMarkers(ship);
            markers.End();
        }

        // Planets by their height above the surface, and the sector by its distance while the player is away. Planets are
        // marked over their copies in the far world, which the camera sees where the planets stand.
        private void AddPlaceMarkers(Vector3 ship)
        {
            SectorSpace space = SectorSpace.Active;
            if (space == null)
                return;

            double3 position = space.WorldToSystem(ship);
            int heading = cruise != null ? cruise.HeadingIndex : CruiseDrive.NoHeading;

            BackdropBodies bodies = BackdropBodies.Active;
            if (bodies != null)
            {
                RefreshPlanetLabels(bodies);
                for (int i = 0; i < bodies.Plans.Count && i < planetLabels.Length; ++i)
                {
                    Transform planet = bodies.InstanceOf(i);
                    if (planet == null)
                        continue;

                    double altitude = CruiseMath.Altitude(position, bodies.CentreOf(i), bodies.RadiusOf(i));
                    Color color = i == heading ? HudFactory.CruiseColor : HudFactory.Muted;
                    markers.Add(planet.position, PlanetMarkerStyle, color, planetLabels[i], Kilometres(altitude));
                }
            }

            if (cruise == null || !cruise.IsAway)
                return;

            SectorGrid grid = space.Grid;
            Vector3 centre = grid.Min + grid.Size * 0.5f;
            double3 min = (double3)(float3)grid.Min;
            double distance = CruiseMath.DistanceToBox(position, min, min + (double3)(float3)grid.Size);
            Color homeColor = heading == CruiseDrive.HomeHeading ? HudFactory.CruiseColor : HudFactory.JumpColor;
            markers.Add(space.SystemToWorld((double3)(float3)centre), HomeMarkerStyle, homeColor, homeLabel, Kilometres(distance));
        }

        // Labels are built once per system, so markers allocate nothing per frame.
        private void RefreshPlanetLabels(BackdropBodies bodies)
        {
            if (labelledBuild == bodies.BuildCount && planetLabels.Length == bodies.Plans.Count)
                return;

            labelledBuild = bodies.BuildCount;
            planetLabels = new string[bodies.Plans.Count];
            for (int i = 0; i < planetLabels.Length; ++i)
                planetLabels[i] = bodies.NameOf(i).ToUpperInvariant();
        }

        private static int Kilometres(double metres)
        {
            return (int)Math.Round(Math.Max(0.0, metres) / MetresPerKilometre);
        }

        // Text is rebuilt only when the state or its whole figure changes; the meter moves every frame.
        private void UpdateCruise()
        {
            int state;
            int figure;
            float fraction;
            switch (cruise.State)
            {
                case CruiseState.Charging:
                    state = CruiseCharging;
                    figure = Mathf.CeilToInt(cruise.ChargeRemaining);
                    fraction = cruise.ChargeProgress;
                    break;
                case CruiseState.Cruising:
                    state = CruiseCruising;
                    figure = (int)Math.Round(cruise.Speed / MetresPerKilometre);
                    fraction = SpeedFraction(cruise.Speed, cruise.Settings);
                    break;
                default:
                    state = cruise.IsBelowFloor ? CruiseAltitude : CruiseReady;
                    figure = state == CruiseAltitude ? AltitudeFigure() : cruise.HeadingIndex;
                    fraction = state == CruiseAltitude ? Mathf.Clamp01((float)(figure / cruise.Settings.DropHeight)) : 0f;
                    break;
            }

            Color color = HudFactory.CruiseColor;
            if (state == displayedCruiseState && figure == displayedCruiseFigure)
            {
                view.SetCruiseProgress(fraction, color);
                return;
            }

            displayedCruiseState = state;
            displayedCruiseFigure = figure;
            switch (state)
            {
                case CruiseCharging:
                    view.SetCruise("CHARGING", color, $"{figure} s", "CANCEL", fraction);
                    break;
                case CruiseCruising:
                    view.SetCruise("CRUISE", color, $"{figure:N0} km/s", "DROP", fraction);
                    break;
                case CruiseAltitude:
                    view.SetCruise("ALTITUDE", color, $"{figure:N0} m", "CLIMB", fraction);
                    break;
                default:
                    view.SetCruise("READY", color, HeadingLabel(), "ENGAGE", fraction);
                    break;
            }
        }

        // Height above the ground below, in steps of 10 m so the text is not rebuilt every frame: by the planet's ground
        // once it is solid, else above the nearest surface.
        private int AltitudeFigure()
        {
            PlanetGround ground = PlanetGround.Active;
            double metres = ground != null && ground.HasGround ? ground.GroundDistance : cruise.Clearance;
            if (double.IsInfinity(metres) || double.IsNaN(metres))
                metres = cruise.Clearance;

            return (int)(Math.Round(Math.Max(0.0, metres) / AltitudeStep) * AltitudeStep);
        }

        private string HeadingLabel()
        {
            int heading = cruise.HeadingIndex;
            if (heading == CruiseDrive.HomeHeading)
                return homeLabel ?? "HOME";

            BackdropBodies bodies = BackdropBodies.Active;
            return heading >= 0 && bodies != null ? bodies.NameOf(heading).ToUpperInvariant() : "";
        }

        // Speed on a log scale between the drive's slowest and fastest, so the meter moves through the whole trip.
        private static float SpeedFraction(double speed, in CruiseSettings settings)
        {
            double span = Math.Log(Math.Max(settings.MaxSpeed, settings.MinSpeed * 1.0001) / settings.MinSpeed);
            return Mathf.Clamp01((float)(Math.Log(Math.Max(speed, settings.MinSpeed) / settings.MinSpeed) / span));
        }

        private void OnCruiseCancelled()
        {
            ShowNotice("CRUISE CANCELLED", HudFactory.WarningColor, false);
        }

        private void OnCruiseDropped(CruiseExit exit)
        {
            switch (exit)
            {
                case CruiseExit.Planet:
                    BackdropBodies bodies = BackdropBodies.Active;
                    string planet = bodies != null && cruise.NearestPlanet >= 0 ? bodies.NameOf(cruise.NearestPlanet).ToUpperInvariant() : "";
                    ShowNotice($"ARRIVED  {planet}", HudFactory.CruiseColor, false);
                    break;
                case CruiseExit.Home:
                    ShowNotice($"ARRIVED  {homeLabel ?? "HOME"}", HudFactory.CruiseColor, false);
                    break;
                case CruiseExit.Collision:
                    ShowNotice("CRUISE INTERRUPTED", HudFactory.WarningColor, true);
                    break;
                default:
                    ShowNotice("CRUISE DISENGAGED", HudFactory.Muted, false);
                    break;
            }
        }

        private static int RoundDistance(float metres)
        {
            return Mathf.RoundToInt(metres / DistanceStep) * DistanceStep;
        }

        private static Color PointColor(ExtractionPointState state)
        {
            switch (state)
            {
                case ExtractionPointState.Uplinking:
                    return HudFactory.CargoColor;
                case ExtractionPointState.Open:
                    return HudFactory.SuccessColor;
                default:
                    return HudFactory.JumpColor;
            }
        }

        private static string PointMarkerLabel(ExtractionPointState state)
        {
            switch (state)
            {
                case ExtractionPointState.Uplinking:
                    return PointMarkerUplink;
                case ExtractionPointState.Open:
                    return PointMarkerOpen;
                default:
                    return PointMarkerDormant;
            }
        }

        // Follows the nearest extraction point while the player flies a ship with a hold.
        private void UpdatePoint()
        {
            ExtractionPoint point = hold != null ? ExtractionPoint.FindNearest(vehicle.transform.position) : null;
            bool show = point != null;
            if (show != hasPointPlate)
            {
                hasPointPlate = show;
                displayedPointState = -1;
                Layout();
            }

            if (!show)
                return;

            Vector3 shipPosition = vehicle.transform.position;
            int distance = Mathf.RoundToInt(point.DistanceTo(shipPosition) / DistanceStep) * DistanceStep;
            int state;
            Color color;
            switch (point.State)
            {
                case ExtractionPointState.Uplinking:
                    state = PointUplink;
                    color = HudFactory.CargoColor;
                    break;
                case ExtractionPointState.Open:
                    state = PointOpen;
                    color = HudFactory.SuccessColor;
                    break;
                default:
                    bool inRange = point.IsInActivationRange(shipPosition);
                    state = inRange ? PointInRange : PointDormant;
                    color = inRange ? HudFactory.JumpColor : HudFactory.Muted;
                    break;
            }

            bool dormant = state == PointDormant || state == PointInRange;
            int value = dormant ? distance : Mathf.CeilToInt(point.TimeRemaining);
            if (state != displayedPointState || value != displayedPointValue || distance != displayedPointDistance)
            {
                displayedPointState = state;
                displayedPointValue = value;
                displayedPointDistance = distance;
                view.SetPoint(PointStatus(state, distance), color, dormant ? $"{distance} m" : $"{value} s",
                    dormant, state == PointInRange && point.CanStartUplink);
            }

            view.SetPointProgress(point.Progress, color);
        }

        private static string PointStatus(int state, int distance)
        {
            switch (state)
            {
                case PointUplink:
                    return $"UPLINK  {distance} m";
                case PointOpen:
                    return $"OPEN  {distance} m";
                case PointInRange:
                    return "IN RANGE";
                default:
                    return "DORMANT";
            }
        }

        private void OnPointStateChanged(ExtractionPoint point)
        {
            if (hold == null)
                return;

            displayedPointState = -1;
            int seconds = Mathf.CeilToInt(point.TimeRemaining);
            switch (point.State)
            {
                case ExtractionPointState.Uplinking:
                    ShowNotice($"UPLINK STARTED  ·  Opens in {seconds} s  ·  Position broadcast", HudFactory.WarningColor, true);
                    break;
                case ExtractionPointState.Open:
                    ShowNotice($"EXTRACTION POINT OPEN  ·  {seconds} s to fly in", HudFactory.SuccessColor, true);
                    break;
                default:
                    ShowNotice("EXTRACTION POINT CLOSED  ·  Uplink again", HudFactory.WarningColor, true);
                    break;
            }
        }

        private static string SpoolStatus(int state)
        {
            switch (state)
            {
                case DisruptedState:
                    return "DISRUPTED";
                case HeldState:
                    return "TOO FAST";
                default:
                    return "SPOOLING";
            }
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
            if (item.Slot == CargoSlot.DarkMatter)
                ShowNotice($"+{count}  {item.DisplayName}   ·   {hold.DarkMatterUnits} units aboard", HudFactory.DarkMatterColor, false);
            else
                ShowNotice($"+{count}  {item.DisplayName}   ·   +{item.UnitMass * count:0.#} t", HudFactory.CargoColor, false);
        }

        private void OnFuelShortage()
        {
            ShowNotice($"NOT ENOUGH DARK MATTER  ·  {jumpDrive.DarkMatterAboard} / {jumpDrive.DarkMatterPerJump}", HudFactory.WarningColor, true);
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
            heldNoticeShown = false;
            RefreshJumpDrive();
            ShowNotice($"JUMP DRIVE SPOOLING  ·  Survive {Mathf.CeilToInt(jumpDrive.SpoolDuration)} s", HudFactory.JumpColor, true);
        }

        // Explained once per spool, so speeding around the limit does not repeat it.
        private void OnSpoolHeldChanged(bool held)
        {
            if (!held || heldNoticeShown)
                return;

            heldNoticeShown = true;
            ShowNotice($"TOO FAST TO SPOOL  ·  Slow below {jumpDrive.MaxSpoolSpeed:0} m/s", HudFactory.WarningColor, true);
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
            view.Layout(heat != null, hold != null, jumpDrive != null, cruise != null, hasPointPlate, manifestOpen, noticeVisible);
            markers.SetCoveredCorner(view.StackSize);
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
