using System;
using System.Collections.Generic;
using DeadCargo.Cargo;
using DeadCargo.Heat;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Extraction
{
    /// <summary>
    /// Takes the ship out of the sector, burning dark matter from the hold. A spool needs enough dark matter aboard
    /// to start, lasts longer the heavier the hold, and emits heat. It only builds below a speed limit, and hits on
    /// the ship knock it back. The ship jumps, and the dark matter burns, when the spool completes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Vehicle))]
    public sealed class JumpDrive : MonoBehaviour
    {
        [Header("Spool")]
        [Tooltip("Spool time with an empty hold, in seconds.")]
        [SerializeField, Min(0.5f)] private float baseSpoolTime = 12f;

        [Tooltip("Extra spool time per tonne in the hold when the spool starts, in seconds.")]
        [SerializeField, Min(0f)] private float spoolTimePerTonne = 0.2f;

        [Tooltip("The spool only builds below this speed, in metres per second. Above it the spool holds.")]
        [SerializeField, Min(0f)] private float maxSpoolSpeed = 50f;

        [Header("Disruption")]
        [Tooltip("Spool time lost when the ship's hull or shields are hit, in seconds.")]
        [SerializeField, Min(0f)] private float knockbackPerHit = 0.5f;

        [Tooltip("Shortest time between knock-backs, in seconds, so rapid fire stalls the spool rather than draining it at once.")]
        [SerializeField, Min(0f)] private float knockbackInterval = 0.5f;

        [Header("Fuel")]
        [Tooltip("Dark matter burned by each jump, in units. A spool cannot start with less aboard.")]
        [SerializeField, Min(0)] private int darkMatterPerJump = 100;

        [Header("Heat")]
        [Tooltip("Heat emitted per second while spooling, held or not. Continuous heat also holds off decay.")]
        [SerializeField, Min(0f)] private float heatPerSecond = 2f;

        // Hit sources, bound only while spooling. Gathered at spool start, because shield modules mount after Awake.
        private readonly List<DamageReceiver> hitReceivers = new List<DamageReceiver>();
        private Vehicle vehicle;
        private CargoHold hold;
        private float spoolElapsed;
        private float lastKnockbackTime = float.NegativeInfinity;

        /// <summary>Raised when a spool starts.</summary>
        public event Action SpoolStarted;

        /// <summary>Raised when a spool stops before completing.</summary>
        public event Action SpoolCancelled;

        /// <summary>Raised with true when the spool starts holding because the ship is too fast, and false when it resumes.</summary>
        public event Action<bool> HeldChanged;

        /// <summary>Raised with the spool time lost, in seconds, when a hit knocks the spool back.</summary>
        public event Action<float> SpoolDisrupted;

        /// <summary>Raised when a spool completes and the ship leaves.</summary>
        public event Action Jumped;

        /// <summary>Raised when a spool is asked for without enough dark matter aboard.</summary>
        public event Action FuelShortage;

        public bool IsSpooling { get; private set; }
        public bool HasJumped { get; private set; }

        /// <summary>True while a spool is active but not building, because the ship is above the speed limit.</summary>
        public bool IsHeld { get; private set; }

        /// <summary>Speed limit for the spool to build, in metres per second.</summary>
        public float MaxSpoolSpeed => maxSpoolSpeed;

        /// <summary>Seconds since a hit last knocked the spool back. Infinite when it never has during this spool.</summary>
        public float TimeSinceKnockback => Time.time - lastKnockbackTime;

        /// <summary>True when a spool can start: the drive is idle and fuelled, and the ship is intact and occupied.</summary>
        public bool CanSpool => IsReady && HasFuel;

        /// <summary>Dark matter each jump burns, in units.</summary>
        public int DarkMatterPerJump => darkMatterPerJump;

        /// <summary>Dark matter in the hold's slot, in units.</summary>
        public int DarkMatterAboard => hold != null ? hold.DarkMatterUnits : 0;

        /// <summary>True when there is enough dark matter aboard for a jump.</summary>
        public bool HasFuel => DarkMatterAboard >= darkMatterPerJump;

        /// <summary>True when <see cref="CompleteSpool"/> can force a jump, fuel or not. For debug tools.</summary>
        public bool CanCompleteSpool => IsSpooling || IsReady;

        // Everything a spool needs except fuel.
        private bool IsReady => isActiveAndEnabled && !IsSpooling && !HasJumped && !vehicle.Destroyed && vehicle.Occupants.Count > 0;

        /// <summary>Length of the current spool, in seconds.</summary>
        public float SpoolDuration { get; private set; }

        /// <summary>Time left on the current spool, in seconds. Zero when idle.</summary>
        public float SpoolRemaining => IsSpooling ? Mathf.Max(0f, SpoolDuration - spoolElapsed) : 0f;

        /// <summary>Progress of the current spool, 0 to 1. Zero when idle.</summary>
        public float SpoolProgress => IsSpooling ? Mathf.Clamp01(spoolElapsed / SpoolDuration) : 0f;

        /// <summary>Spool time if a spool started now, in seconds.</summary>
        public float EstimatedSpoolTime => baseSpoolTime + (hold != null ? hold.TotalMass * spoolTimePerTonne : 0f);

        private float Speed => vehicle.CachedRigidbody != null ? vehicle.CachedRigidbody.linearVelocity.magnitude : 0f;

        public bool StartSpool()
        {
            if (!IsReady)
                return false;

            if (!HasFuel)
            {
                FuelShortage?.Invoke();
                return false;
            }

            BeginSpool();
            return true;
        }

        public void CancelSpool()
        {
            if (!IsSpooling)
                return;

            StopSpool();
            SpoolCancelled?.Invoke();
        }

        public void ToggleSpool()
        {
            if (IsSpooling)
                CancelSpool();
            else
                StartSpool();
        }

        /// <summary>Finishes the spool now, starting one first if needed, even without enough dark matter. For debug tools.</summary>
        public void CompleteSpool()
        {
            if (!IsSpooling && !IsReady)
                return;

            if (!IsSpooling)
                BeginSpool();
            Jump();
        }

        private void Awake()
        {
            vehicle = GetComponent<Vehicle>();
            hold = GetComponent<CargoHold>();

            vehicle.onExitedAll.AddListener(CancelSpool);
            vehicle.onDestroyed.AddListener(CancelSpool);
        }

        private void OnDestroy()
        {
            if (vehicle != null)
            {
                vehicle.onExitedAll.RemoveListener(CancelSpool);
                vehicle.onDestroyed.RemoveListener(CancelSpool);
            }

            UnbindHits();
        }

        private void OnDisable()
        {
            // Stops without the event: during scene unload its listeners may already be gone.
            StopSpool();
        }

        private void Update()
        {
            if (!IsSpooling)
                return;

            if (heatPerSecond > 0f && HeatTracker.Active != null)
                HeatTracker.Active.Add(heatPerSecond * Time.deltaTime);

            bool held = Speed > maxSpoolSpeed;
            if (held != IsHeld)
            {
                IsHeld = held;
                HeldChanged?.Invoke(held);
            }

            if (!held)
                spoolElapsed += Time.deltaTime;

            if (spoolElapsed >= SpoolDuration)
                Jump();
        }

        private void OnDamaged(float damage, Vector3 hitPoint, HealthModifierType modifierType, Transform source)
        {
            if (!IsSpooling || knockbackPerHit <= 0f || Time.time - lastKnockbackTime < knockbackInterval)
                return;

            lastKnockbackTime = Time.time;
            float lost = Mathf.Min(knockbackPerHit, spoolElapsed);
            spoolElapsed -= lost;
            SpoolDisrupted?.Invoke(lost);
        }

        private void BeginSpool()
        {
            SpoolDuration = EstimatedSpoolTime;
            spoolElapsed = 0f;
            lastKnockbackTime = float.NegativeInfinity;
            IsHeld = false;
            IsSpooling = true;
            BindHits();
            SpoolStarted?.Invoke();
        }

        // The fuel burns before the event, so whatever is left is what the run banks.
        private void Jump()
        {
            StopSpool();
            if (hold != null)
                hold.ConsumeDarkMatter(darkMatterPerJump);

            HasJumped = true;
            Jumped?.Invoke();
        }

        private void StopSpool()
        {
            IsSpooling = false;
            IsHeld = false;
            UnbindHits();
        }

        // Damage receivers report every hit on the hull and shield colliders, even when the ship cannot take damage,
        // so the spool reacts to fire with the invulnerability cheat on too.
        private void BindHits()
        {
            UnbindHits();
            GetComponentsInChildren(true, hitReceivers);
            foreach (DamageReceiver receiver in hitReceivers)
                receiver.onDamaged.AddListener(OnDamaged);
        }

        private void UnbindHits()
        {
            foreach (DamageReceiver receiver in hitReceivers)
            {
                if (receiver != null)
                    receiver.onDamaged.RemoveListener(OnDamaged);
            }

            hitReceivers.Clear();
        }
    }
}
