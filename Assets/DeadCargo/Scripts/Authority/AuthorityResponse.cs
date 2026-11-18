using System;
using System.Collections.Generic;
using DeadCargo.AI;
using DeadCargo.Heat;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Authority
{
    /// <summary>
    /// Sends Authority ships after the player according to the current heat tier.
    /// Each tier sets how many ships may hunt at once and how fast reinforcements arrive. A ship that stops hunting,
    /// because heat fell away or its pilot gave up the search, withdraws: it flies off and leaves the scene once it is
    /// out of sight or far away, and its place in the hunt is free at once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuthorityResponse : MonoBehaviour
    {
        // How long a ship leaving alive stays switched off before it is destroyed, in seconds: every radar has let go by then.
        private const float RadarReleaseTime = 1f;

        /// <summary>A spawned ship, the AI pilot flying it, and that pilot's brain if it is a Dead Cargo pilot.</summary>
        private sealed class Unit
        {
            public GameAgent Pilot;
            public Vehicle Ship;
            public PilotBrain Brain;
        }

        [Header("Units")]
        [Tooltip("AI pilot prefab that flies each Authority ship. A Dead Cargo pilot (with a Pilot Brain) withdraws; any other pilot's ship is removed at once.")]
        [SerializeField] private GameAgent pilotPrefab;

        [Tooltip("Ship prefab the Authority sends.")]
        [SerializeField] private Vehicle shipPrefab;

        [Header("Response")]
        [Tooltip("One entry per heat tier, in tier order. Higher tiers reuse the last entry.")]
        [SerializeField] private AuthorityTierResponse[] tierResponses =
        {
            new AuthorityTierResponse(0, 10f),
            new AuthorityTierResponse(1, 10f),
            new AuthorityTierResponse(2, 7f),
            new AuthorityTierResponse(4, 5f)
        };

        [Tooltip("Delay before the first ship arrives after heat enters a higher tier, in seconds.")]
        [SerializeField, Min(0f)] private float escalationDelay = 2f;

        [Tooltip("Time heat must stay in a tier with no ships allowed before the hunting ships withdraw, in seconds.")]
        [SerializeField, Min(0f)] private float standDownDelay = 10f;

        [Header("Spawning")]
        [Tooltip("Distance from the player at which ships arrive, in metres.")]
        [SerializeField, Min(50f)] private float spawnDistance = 400f;

        [Tooltip("Time a destroyed ship's wreck stays in the scene, in seconds.")]
        [SerializeField, Min(0f)] private float wreckLifetime = 6f;

        [Header("Withdrawal")]
        [Tooltip("A withdrawing ship leaves once it is this far from the camera, in view or not, in metres.")]
        [SerializeField, Min(100f)] private float leaveDistance = 2500f;

        [Tooltip("A withdrawing ship leaves sooner once it is out of view and at least this far from the camera, in metres.")]
        [SerializeField, Min(0f)] private float vanishDistance = 600f;

        [Header("Heat")]
        [Tooltip("Heat added when an Authority ship is destroyed, hunting or withdrawing.")]
        [SerializeField, Min(0f)] private float heatPerShipDestroyed = 10f;

        private readonly List<Unit> units = new List<Unit>();
        private readonly List<Vehicle> activeShips = new List<Vehicle>();
        private readonly List<Unit> withdrawing = new List<Unit>();
        private readonly List<Vehicle> withdrawingShips = new List<Vehicle>();
        private readonly PilotWing wing = new PilotWing();
        private HeatTracker tracker;
        private float nextSpawnTime;
        private float standDownTime = float.PositiveInfinity;

        /// <summary>Raised when a ship arrives.</summary>
        public event Action<Vehicle> ShipSpawned;

        /// <summary>Raised when a ship is destroyed, hunting or withdrawing.</summary>
        public event Action<Vehicle> ShipDestroyed;

        /// <summary>Raised when a ship stops hunting and starts to withdraw.</summary>
        public event Action<Vehicle> ShipWithdrawing;

        /// <summary>The response for the loaded scene, or null.</summary>
        public static AuthorityResponse Active { get; private set; }

        /// <summary>The number of ships hunting the player. Withdrawing ships do not count.</summary>
        public int ActiveShipCount => units.Count;

        /// <summary>The ships hunting the player. Destroyed and withdrawing ships leave the list at once.</summary>
        public IReadOnlyList<Vehicle> ActiveShips => activeShips;

        /// <summary>Ships flying away after leaving the hunt. They leave the list when they leave the scene or are destroyed.</summary>
        public IReadOnlyList<Vehicle> WithdrawingShips => withdrawingShips;

        /// <summary>The response for the current heat tier.</summary>
        public AuthorityTierResponse CurrentResponse => GetResponse(tracker != null ? tracker.TierIndex : 0);

        /// <summary>When false, no ships are sent automatically. Ships already active are unaffected.</summary>
        public bool SpawningEnabled { get; set; } = true;

        /// <summary>The hunters' wing: they fan out round the player and hold fire when a wingman is in the way.</summary>
        public PilotWing Wing => wing;

        /// <summary>Sends one ship now with the current tier's pilot skill, ignoring the tier limit. Returns null if it cannot spawn.</summary>
        public Vehicle SpawnShip()
        {
            return SpawnShip(CurrentResponse.PilotSkill);
        }

        /// <summary>Sends one ship now flown at the given skill (null: Regular), ignoring the tier limit. Returns null if it cannot spawn.</summary>
        public Vehicle SpawnShip(PilotSkillDefinition skill)
        {
            Vehicle target = FindPlayerVehicle();
            if (pilotPrefab == null || shipPrefab == null || target == null)
                return null;

            // Arrive on a sphere around the player, kept to the upper half so nothing spawns below ground.
            Vector3 direction = UnityEngine.Random.onUnitSphere;
            direction.y = Mathf.Abs(direction.y);

            Vector3 position = target.transform.position + direction * spawnDistance;
            Quaternion facing = Quaternion.LookRotation(-direction);

            // Spawned at the scene root: the kit finds a ship's parts through transform.root,
            // so ships parented here would share one root and break radar and hit detection.
            var unit = new Unit
            {
                Ship = Instantiate(shipPrefab, position, facing),
                Pilot = Instantiate(pilotPrefab)
            };

            // Drawn between physics steps, so hunters glide rather than step under the smooth chase camera.
            if (unit.Ship.TryGetComponent(out Rigidbody shipBody))
                shipBody.interpolation = RigidbodyInterpolation.Interpolate;

            unit.Pilot.EnterVehicle(unit.Ship);
            unit.Brain = unit.Pilot.GetComponentInChildren<PilotBrain>();
            if (unit.Brain != null)
            {
                unit.Brain.SetSkill(skill);
                unit.Brain.JoinWing(wing);
                wing.Join(unit.Brain);
                unit.Brain.GaveUp += () => OnPilotGaveUp(unit);
            }

            unit.Ship.onDestroyed.AddListener(() => OnShipDestroyed(unit));
            units.Add(unit);
            activeShips.Add(unit.Ship);

            ShipSpawned?.Invoke(unit.Ship);
            return unit.Ship;
        }

        /// <summary>Takes every hunting ship off the hunt. Each flies away and leaves once out of sight.</summary>
        public void WithdrawAll()
        {
            for (int i = units.Count - 1; i >= 0; --i)
                Withdraw(units[i]);
        }

        /// <summary>Removes every Authority ship at once, hunting or withdrawing, without awarding heat. For debug tools.</summary>
        public void DespawnAll()
        {
            foreach (Unit unit in units)
                Release(unit, 0f);
            foreach (Unit unit in withdrawing)
                Release(unit, 0f);

            units.Clear();
            activeShips.Clear();
            withdrawing.Clear();
            withdrawingShips.Clear();
            wing.Clear();
        }

        /// <summary>Fills the list with the pilot skills the tiers send, each once, in tier order. Empty tiers are left out.</summary>
        public void GetPilotSkills(List<PilotSkillDefinition> skills)
        {
            skills.Clear();
            if (tierResponses == null)
                return;

            foreach (AuthorityTierResponse response in tierResponses)
            {
                if (response.PilotSkill != null && !skills.Contains(response.PilotSkill))
                    skills.Add(response.PilotSkill);
            }
        }

        /// <summary>The Dead Cargo pilot flying an Authority ship, hunting or withdrawing. Null for other ships and for the kit's own pilots.</summary>
        public PilotBrain PilotOf(Vehicle ship)
        {
            foreach (Unit unit in units)
            {
                if (unit.Ship == ship)
                    return unit.Brain;
            }

            foreach (Unit unit in withdrawing)
            {
                if (unit.Ship == ship)
                    return unit.Brain;
            }

            return null;
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
            tracker = HeatTracker.Active;
            if (tracker == null)
            {
                Debug.LogWarning("Authority response needs a Heat Tracker in the scene.", this);
                enabled = false;
                return;
            }

            tracker.TierChanged += OnTierChanged;
        }

        private void OnDestroy()
        {
            if (tracker != null)
                tracker.TierChanged -= OnTierChanged;
        }

        private void Update()
        {
            UpdateWithdrawals();

            AuthorityTierResponse response = CurrentResponse;

            if (response.MaxShips == 0)
            {
                UpdateStandDown();
                return;
            }

            standDownTime = float.PositiveInfinity;

            if (!SpawningEnabled || units.Count >= response.MaxShips || Time.time < nextSpawnTime)
                return;

            if (SpawnShip() != null)
                nextSpawnTime = Time.time + response.SpawnInterval;
        }

        // Hunting ships withdraw once heat has stayed in a no-response tier for the stand-down delay.
        private void UpdateStandDown()
        {
            if (units.Count == 0)
                return;

            if (float.IsPositiveInfinity(standDownTime))
                standDownTime = Time.time + standDownDelay;
            else if (Time.time >= standDownTime)
                WithdrawAll();
        }

        // A withdrawing ship leaves the scene once it is far from the camera, or out of view and past the vanish distance.
        // Ships asleep with the sector are left alone: they carry on leaving when the player is back.
        private void UpdateWithdrawals()
        {
            if (withdrawing.Count == 0)
                return;

            Camera view = Camera.main;
            Vector3 observer = ObserverPosition(view);
            for (int i = withdrawing.Count - 1; i >= 0; --i)
            {
                Unit unit = withdrawing[i];
                if (unit.Ship != null && (!unit.Ship.gameObject.activeInHierarchy || !HasLeftSight(unit.Ship.transform.position, view, observer)))
                    continue;

                withdrawing.RemoveAt(i);
                withdrawingShips.Remove(unit.Ship);
                Release(unit, 0f);
            }
        }

        private bool HasLeftSight(Vector3 position, Camera view, Vector3 observer)
        {
            float sqrDistance = (position - observer).sqrMagnitude;
            if (sqrDistance >= leaveDistance * leaveDistance)
                return true;
            if (view == null || sqrDistance < vanishDistance * vanishDistance)
                return false;

            Vector3 viewport = view.WorldToViewportPoint(position);
            return viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f;
        }

        private void OnTierChanged(int tierIndex)
        {
            if (GetResponse(tierIndex).MaxShips <= units.Count)
                return;

            // Escalation brings the next ship in after a short delay instead of waiting out the old interval.
            float earliest = Time.time + escalationDelay;
            nextSpawnTime = nextSpawnTime < Time.time ? earliest : Mathf.Min(nextSpawnTime, earliest);
        }

        // A pilot that lost the player and searched in vain leaves the hunt; its replacement waits out the tier's interval.
        private void OnPilotGaveUp(Unit unit)
        {
            if (!units.Contains(unit))
                return;

            Withdraw(unit);
            nextSpawnTime = Mathf.Max(nextSpawnTime, Time.time + CurrentResponse.SpawnInterval);
        }

        private void Withdraw(Unit unit)
        {
            if (!units.Remove(unit))
                return;

            activeShips.Remove(unit.Ship);
            wing.Leave(unit.Brain);

            // The kit's own pilots cannot be ordered away, so their ships go at once, as before Dead Cargo pilots.
            if (unit.Brain == null || unit.Ship == null)
            {
                Release(unit, 0f);
                return;
            }

            withdrawing.Add(unit);
            withdrawingShips.Add(unit.Ship);
            unit.Brain.Withdraw(ObserverPosition(Camera.main));
            ShipWithdrawing?.Invoke(unit.Ship);
        }

        private void OnShipDestroyed(Unit unit)
        {
            if (units.Remove(unit))
            {
                activeShips.Remove(unit.Ship);
                wing.Leave(unit.Brain);
            }
            else if (withdrawing.Remove(unit))
            {
                withdrawingShips.Remove(unit.Ship);
            }
            else
                return;

            Release(unit, wreckLifetime);

            if (tracker != null)
                tracker.Add(heatPerShipDestroyed);

            ShipDestroyed?.Invoke(unit.Ship);
        }

        // The kit's radar never forgets a trackable, and a ship destroyed while any radar tracks it breaks that radar for
        // good: it throws every frame from then on. A kill is safe, because the kit switches the ship off when it
        // detonates. A ship leaving alive is switched off here the same way, so every radar lets go of it cleanly,
        // and destroyed a moment later.
        private static void Release(Unit unit, float delay)
        {
            if (delay <= 0f)
            {
                if (unit.Ship != null && unit.Ship.gameObject.activeSelf)
                {
                    unit.Ship.gameObject.SetActive(false);
                    delay = RadarReleaseTime;
                }

                if (unit.Pilot != null)
                    unit.Pilot.gameObject.SetActive(false);
            }

            if (unit.Pilot != null)
                Destroy(unit.Pilot.gameObject, delay);
            if (unit.Ship != null)
                Destroy(unit.Ship.gameObject, delay);
        }

        private AuthorityTierResponse GetResponse(int tierIndex)
        {
            if (tierResponses == null || tierResponses.Length == 0)
                return default;

            return tierResponses[Mathf.Clamp(tierIndex, 0, tierResponses.Length - 1)];
        }

        // Ships withdraw from, and leave the sight of, the camera; without one, the player's vehicle.
        private static Vector3 ObserverPosition(Camera view)
        {
            if (view != null)
                return view.transform.position;

            Vehicle player = FindPlayerVehicle();
            return player != null ? player.transform.position : Vector3.zero;
        }

        private static Vehicle FindPlayerVehicle()
        {
            GameAgentManager manager = GameAgentManager.Instance;
            GameAgent player = manager != null ? manager.FocusedGameAgent : null;
            return player != null ? player.Vehicle : null;
        }
    }
}
