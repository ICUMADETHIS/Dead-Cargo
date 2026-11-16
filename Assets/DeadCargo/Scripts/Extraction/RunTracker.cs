using System;
using System.Collections.Generic;
using DeadCargo.Authority;
using DeadCargo.Cargo;
using DeadCargo.Flow;
using DeadCargo.Heat;
using DeadCargo.Profile;
using UnityEngine;
using UnityEngine.Events;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Extraction
{
    /// <summary>
    /// Decides how the run ends. The run ship is the last ship with a jump drive that the player boarded.
    /// The run is extracted when that ship jumps, and lost when it is destroyed or the player dies.
    /// A lost run empties the ship's hold. Every run is recorded to the profile, and an extracted hold is banked into the stash.
    /// The tracker owns the end of the run: it replaces the kit's game-over flow and locks all input once the run is over.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunTracker : MonoBehaviour
    {
        private const string RestartTitle = "RESTARTING RUN";

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

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
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

            authority = FindFirstObjectByType<AuthorityResponse>();
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
            End(RunOutcome.Extracted);
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

        private void End(RunOutcome outcome)
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

            HeatTier? peakHeat = heat != null ? heat.Tiers[peakTierIndex] : (HeatTier?)null;
            Result = new RunResult(outcome, Time.time - startTime, peakHeat, authorityShipsDestroyed, cargo);
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
            var summary = new RunSummary(extracted, result.Duration, peakHeat, result.AuthorityShipsDestroyed, result.CargoValue, result.CargoMass);
            ProfileStore.RecordRun(summary, extracted ? result.Cargo : null);
        }

        // Covers the kit's menu controls too: resuming from its pause menu would unfreeze the world and re-enable the player.
        private static void DisableAllInput()
        {
            foreach (GeneralInput input in FindObjectsByType<GeneralInput>(FindObjectsSortMode.None))
                input.DisableInput();
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
