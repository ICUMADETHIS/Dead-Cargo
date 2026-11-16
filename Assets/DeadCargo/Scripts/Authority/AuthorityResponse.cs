using System;
using System.Collections.Generic;
using DeadCargo.Heat;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Authority
{
    /// <summary>
    /// Sends Authority ships after the player according to the current heat tier.
    /// Each tier sets how many ships may be active and how fast reinforcements arrive.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuthorityResponse : MonoBehaviour
    {
        /// <summary>A spawned ship and the AI pilot flying it.</summary>
        private sealed class Unit
        {
            public GameAgent Pilot;
            public Vehicle Ship;
        }

        [Header("Units")]
        [Tooltip("AI pilot prefab that flies each Authority ship.")]
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

        [Tooltip("Time heat must stay in a tier with no ships allowed before active ships withdraw, in seconds.")]
        [SerializeField, Min(0f)] private float standDownDelay = 10f;

        [Header("Spawning")]
        [Tooltip("Distance from the player at which ships arrive, in metres.")]
        [SerializeField, Min(50f)] private float spawnDistance = 400f;

        [Tooltip("Time a destroyed ship's wreck stays in the scene, in seconds.")]
        [SerializeField, Min(0f)] private float wreckLifetime = 6f;

        [Header("Heat")]
        [Tooltip("Heat added when an Authority ship is destroyed.")]
        [SerializeField, Min(0f)] private float heatPerShipDestroyed = 10f;

        private readonly List<Unit> units = new List<Unit>();
        private HeatTracker tracker;
        private float nextSpawnTime;
        private float standDownTime = float.PositiveInfinity;

        /// <summary>Raised when a ship arrives.</summary>
        public event Action<Vehicle> ShipSpawned;

        /// <summary>Raised when a ship is destroyed.</summary>
        public event Action<Vehicle> ShipDestroyed;

        public int ActiveShipCount => units.Count;

        /// <summary>The response for the current heat tier.</summary>
        public AuthorityTierResponse CurrentResponse => GetResponse(tracker != null ? tracker.TierIndex : 0);

        /// <summary>When false, no ships are sent automatically. Ships already active are unaffected.</summary>
        public bool SpawningEnabled { get; set; } = true;

        /// <summary>Sends one ship now, ignoring the tier limit. Returns null if it cannot spawn.</summary>
        public Vehicle SpawnShip()
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

            unit.Pilot.EnterVehicle(unit.Ship);
            unit.Ship.onDestroyed.AddListener(() => OnShipDestroyed(unit));
            units.Add(unit);

            ShipSpawned?.Invoke(unit.Ship);
            return unit.Ship;
        }

        /// <summary>Removes every active Authority ship without awarding heat.</summary>
        public void DespawnAll()
        {
            foreach (Unit unit in units)
                Release(unit, 0f);

            units.Clear();
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

        // Ships withdraw once heat has stayed in a no-response tier for the stand-down delay.
        private void UpdateStandDown()
        {
            if (units.Count == 0)
                return;

            if (float.IsPositiveInfinity(standDownTime))
                standDownTime = Time.time + standDownDelay;
            else if (Time.time >= standDownTime)
                DespawnAll();
        }

        private void OnTierChanged(int tierIndex)
        {
            if (GetResponse(tierIndex).MaxShips <= units.Count)
                return;

            // Escalation brings the next ship in after a short delay instead of waiting out the old interval.
            float earliest = Time.time + escalationDelay;
            nextSpawnTime = nextSpawnTime < Time.time ? earliest : Mathf.Min(nextSpawnTime, earliest);
        }

        private void OnShipDestroyed(Unit unit)
        {
            if (!units.Remove(unit))
                return;

            Release(unit, wreckLifetime);

            if (tracker != null)
                tracker.Add(heatPerShipDestroyed);

            ShipDestroyed?.Invoke(unit.Ship);
        }

        private static void Release(Unit unit, float delay)
        {
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

        private static Vehicle FindPlayerVehicle()
        {
            GameAgentManager manager = GameAgentManager.Instance;
            GameAgent player = manager != null ? manager.FocusedGameAgent : null;
            return player != null ? player.Vehicle : null;
        }
    }
}
