using System;
using System.Collections.Generic;
using DeadCargo.Authority;
using DeadCargo.Cargo;
using DeadCargo.Flow;
using DeadCargo.Heat;
using DeadCargo.Profile;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Extraction
{
    /// <summary>
    /// Decides how the run ends. The run ship is the last ship with a jump drive that the player boarded.
    /// The run is extracted when that ship jumps, and lost when it is destroyed or the player dies.
    /// A lost run empties the ship's hold, and a destroyed ship costs a replacement fee.
    /// Every run is recorded to the profile, and an extracted hold is banked into the stash.
    /// The tracker owns the end of the run: it replaces the kit's game-over flow and locks all input once the run is over.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunTracker : MonoBehaviour
    {
        private const string RestartTitle = "RESTARTING RUN";

        // Buttons in the kit's pause and game-over menus.
        private const string QuitButtonName = "QuitButton";
        private const string RestartButtonName = "RestartButton";

        [Tooltip("Credits charged when the run ship is destroyed. Never takes credits below zero, so a basic hull is always free. " +
            "Moves to the ship's hull data when hulls arrive in phase 10.")]
        [SerializeField, Min(0)] private int shipReplacementCost = 500;

        private readonly List<int> suppressedDeathCalls = new List<int>();
        private readonly List<UnityEventCallState> suppressedDeathCallStates = new List<UnityEventCallState>();
        private GameAgentManager agents;
        private HeatTracker heat;
        private AuthorityResponse authority;
        private CargoHold hold;
        private float startTime;
        private int peakTierIndex;
        private int authorityShipsDestroyed;

        /// <summary>The tracker for the loaded scene, or null.</summary>
        public static RunTracker Active { get; private set; }

        /// <summary>Raised once, when the run is extracted or lost.</summary>
        public event Action<RunResult> Ended;

        /// <summary>The ship the run extracts with, or null before the player boards one.</summary>
        public Vehicle Ship { get; private set; }

        /// <summary>The run ship's jump drive, or null before the player boards one.</summary>
        public JumpDrive Drive { get; private set; }

        /// <summary>The finished run, or null while the run is in progress.</summary>
        public RunResult Result { get; private set; }

        public bool IsOver => Result != null;

        /// <summary>Time since the run started, in seconds. Stops when the run ends.</summary>
        public float Elapsed => IsOver ? Result.Duration : Time.time - startTime;

        /// <summary>True when the player is flying the run ship.</summary>
        public bool IsPlayerAboard
        {
            get
            {
                GameAgent player = agents != null ? agents.FocusedGameAgent : null;
                return Ship != null && IsPlayer(player) && player.Vehicle == Ship;
            }
        }

        /// <summary>
        /// Ends the run as extracted through an extraction point. Any jump spool is dropped and no dark matter burns.
        /// Returns false when the run is already over.
        /// </summary>
        public bool ExtractAtPoint()
        {
            if (IsOver)
                return false;

            if (Drive != null)
                Drive.CancelSpool();

            End(RunOutcome.Extracted, ExtractionRoute.ExtractionPoint);
            return true;
        }

        /// <summary>Starts a new run by reloading this sector.</summary>
        public void Restart()
        {
            SceneFlow.LoadScene(gameObject.scene.path, RestartTitle);
        }

        /// <summary>Returns to the mothership, or restarts the run when no mothership scene is set up.</summary>
        public void ReturnToMothership()
        {
            if (!SceneFlow.HasMothership)
                Restart();
            else
                SceneFlow.LoadMothership();
        }

        /// <summary>
        /// Gives up the run and returns to the mothership. The run is recorded as lost and the hold is forfeit,
        /// but an intact ship is not charged for. For the pause menu's Quit button.
        /// </summary>
        public void AbandonToMothership()
        {
            End(RunOutcome.Lost);
            ReturnToMothership();
        }

        /// <summary>
        /// Gives up the run and starts a new one in this sector. The run is recorded as lost and the hold is forfeit.
        /// For the pause menu's Restart button.
        /// </summary>
        public void AbandonAndRestart()
        {
            End(RunOutcome.Lost);
            Restart();
        }

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
        }

        private void Awake()
        {
            TakeOverKitMenus();
        }

        private void OnEnable()
        {
            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        private void Start()
        {
            startTime = Time.time;

            agents = GameAgentManager.Instance;
            if (agents == null)
            {
                Debug.LogWarning("Run tracker needs a Game Agent Manager in the scene.", this);
                enabled = false;
                return;
            }

            agents.onFocusedVehicleChanged.AddListener(OnFocusedVehicleChanged);
            agents.onFocusedGameAgentDied.AddListener(OnFocusedAgentDied);
            SuppressKitGameOver();

            GameAgent focused = agents.FocusedGameAgent;
            if (focused != null)
                OnFocusedVehicleChanged(focused.Vehicle);

            heat = HeatTracker.Active;
            if (heat != null)
            {
                peakTierIndex = heat.TierIndex;
                heat.TierChanged += OnTierChanged;
            }

            authority = AuthorityResponse.Active;
            if (authority != null)
                authority.ShipDestroyed += OnAuthorityShipDestroyed;
        }

        private void OnDestroy()
        {
            if (agents != null)
            {
                agents.onFocusedVehicleChanged.RemoveListener(OnFocusedVehicleChanged);
                agents.onFocusedGameAgentDied.RemoveListener(OnFocusedAgentDied);
                RestoreKitGameOver();
            }

            if (heat != null)
                heat.TierChanged -= OnTierChanged;
            if (authority != null)
                authority.ShipDestroyed -= OnAuthorityShipDestroyed;

            UnbindShip();
        }

        private void OnFocusedVehicleChanged(Vehicle vehicle)
        {
            if (IsOver || vehicle == null || vehicle == Ship || !IsPlayer(agents.FocusedGameAgent))
                return;

            // On foot, or in a ship that cannot extract: the current run ship stays.
            if (!vehicle.TryGetComponent(out JumpDrive drive))
                return;

            UnbindShip();
            Ship = vehicle;
            Drive = drive;
            hold = vehicle.GetComponent<CargoHold>();
            Ship.onDestroyed.AddListener(OnShipDestroyed);
            Drive.Jumped += OnJumped;
        }

        private void UnbindShip()
        {
            if (Ship != null)
                Ship.onDestroyed.RemoveListener(OnShipDestroyed);
            if (Drive != null)
                Drive.Jumped -= OnJumped;

            Ship = null;
            Drive = null;
            hold = null;
        }

        private void OnJumped()
        {
            End(RunOutcome.Extracted, ExtractionRoute.JumpDrive);
        }

        private void OnShipDestroyed()
        {
            End(RunOutcome.Lost);
        }

        private void OnFocusedAgentDied()
        {
            if (IsPlayer(agents.FocusedGameAgent))
                End(RunOutcome.Lost);
        }

        private void OnTierChanged(int tierIndex)
        {
            peakTierIndex = Mathf.Max(peakTierIndex, tierIndex);
        }

        private void OnAuthorityShipDestroyed(Vehicle ship)
        {
            if (!IsOver)
                authorityShipsDestroyed++;
        }

        // The route only matters for an extracted run.
        private void End(RunOutcome outcome, ExtractionRoute route = ExtractionRoute.JumpDrive)
        {
            if (IsOver)
                return;

            CargoStack[] cargo = Array.Empty<CargoStack>();
            if (hold != null && !hold.IsEmpty)
            {
                cargo = new CargoStack[hold.Stacks.Count];
                for (int i = 0; i < cargo.Length; ++i)
                    cargo[i] = hold.Stacks[i];
            }

            // Dying on foot with the ship intact is not a ship loss. The fee is capped at what the player holds.
            bool shipLost = outcome == RunOutcome.Lost && Ship != null && Ship.Destroyed;
            int shipLossFee = shipLost ? Mathf.Min(shipReplacementCost, ProfileStore.Current.Credits) : 0;

            HeatTier? peakHeat = heat != null ? heat.Tiers[peakTierIndex] : (HeatTier?)null;
            Result = new RunResult(outcome, route, Time.time - startTime, peakHeat, authorityShipsDestroyed, cargo, shipLost, shipLossFee);
            RecordToProfile(Result);

            if (outcome == RunOutcome.Lost)
            {
                if (Drive != null)
                    Drive.CancelSpool();
                if (hold != null)
                    hold.Clear();
            }

            DisableAllInput();
            Ended?.Invoke(Result);
        }

        // Saved the moment the run ends, so the result and any banked cargo survive quitting from the report.
        private static void RecordToProfile(RunResult result)
        {
            bool extracted = result.Outcome == RunOutcome.Extracted;
            string peakHeat = result.PeakHeat.HasValue ? result.PeakHeat.Value.Label : string.Empty;
            var summary = new RunSummary(extracted, result.Duration, peakHeat, result.AuthorityShipsDestroyed, result.CargoValue, result.CargoMass,
                result.ShipLost, result.ShipLossFee);
            ProfileStore.RecordRun(summary, extracted ? result.Cargo : null);
        }

        // Covers the kit's menu controls too: resuming from its pause menu would unfreeze the world and re-enable the player.
        private static void DisableAllInput()
        {
            foreach (GeneralInput input in FindObjectsByType<GeneralInput>(FindObjectsSortMode.None))
                input.DisableInput();
        }

        // The kit's pause and game-over menus quit to the kit's demo menu and reload the scene directly.
        // Here both give up the run instead. Done in Awake, so a scene with its own menu handling,
        // such as the test range, can rebind the buttons in Start.
        private void TakeOverKitMenus()
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                {
                    if (button.name == QuitButtonName)
                        Rebind(button, AbandonToMothership);
                    else if (button.name == RestartButtonName)
                        Rebind(button, AbandonAndRestart);
                }
            }
        }

        // Replaces the whole click event, which drops the listeners saved on the kit prefab.
        private static void Rebind(Button button, UnityAction action)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
        }

        // The kit switches to its game-over state when the player dies, which opens its own menu over the run report.
        // Only game-state switches are turned off; other death listeners keep running.
        private void SuppressKitGameOver()
        {
            UnityEventBase onDied = agents.onFocusedGameAgentDied;
            for (int i = 0; i < onDied.GetPersistentEventCount(); ++i)
            {
                if (!IsGameStateSwitch(onDied.GetPersistentTarget(i)))
                    continue;

                suppressedDeathCalls.Add(i);
                suppressedDeathCallStates.Add(onDied.GetPersistentListenerState(i));
                onDied.SetPersistentListenerState(i, UnityEventCallState.Off);
            }
        }

        private void RestoreKitGameOver()
        {
            for (int i = 0; i < suppressedDeathCalls.Count; ++i)
                agents.onFocusedGameAgentDied.SetPersistentListenerState(suppressedDeathCalls[i], suppressedDeathCallStates[i]);

            suppressedDeathCalls.Clear();
            suppressedDeathCallStates.Clear();
        }

        // The kit's demo scenes call the manager directly; other scenes go through the GameStateEnter helper.
        private static bool IsGameStateSwitch(UnityEngine.Object target)
        {
            return target is GameStateManager || target is GameStateEnter;
        }

        private static bool IsPlayer(GameAgent agent)
        {
            return agent != null && agent.IsPlayer;
        }
    }
}
