using System;
using DeadCargo.Cargo;
using DeadCargo.Heat;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Extraction
{
    /// <summary>
    /// Takes the ship out of the sector. The drive spools for a time that grows with the hold's load,
    /// emits heat while spooling, and jumps when the spool completes.
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

        [Header("Heat")]
        [Tooltip("Heat emitted per second while spooling. Continuous heat also holds off decay.")]
        [SerializeField, Min(0f)] private float heatPerSecond = 2f;

        private Vehicle vehicle;
        private CargoHold hold;
        private float spoolStartTime;

        /// <summary>Raised when a spool starts.</summary>
        public event Action SpoolStarted;

        /// <summary>Raised when a spool stops before completing.</summary>
        public event Action SpoolCancelled;

        /// <summary>Raised when a spool completes and the ship leaves.</summary>
        public event Action Jumped;

        public bool IsSpooling { get; private set; }
        public bool HasJumped { get; private set; }

        /// <summary>True when a spool can start: the drive is idle, the ship is intact and occupied.</summary>
        public bool CanSpool => isActiveAndEnabled && !IsSpooling && !HasJumped && !vehicle.Destroyed && vehicle.Occupants.Count > 0;

        /// <summary>Length of the current spool, in seconds.</summary>
        public float SpoolDuration { get; private set; }

        /// <summary>Time left on the current spool, in seconds. Zero when idle.</summary>
        public float SpoolRemaining => IsSpooling ? Mathf.Max(0f, SpoolDuration - (Time.time - spoolStartTime)) : 0f;

        /// <summary>Progress of the current spool, 0 to 1. Zero when idle.</summary>
        public float SpoolProgress => IsSpooling ? Mathf.Clamp01((Time.time - spoolStartTime) / SpoolDuration) : 0f;

        /// <summary>Spool time if a spool started now, in seconds.</summary>
        public float EstimatedSpoolTime => baseSpoolTime + (hold != null ? hold.TotalMass * spoolTimePerTonne : 0f);

        public bool StartSpool()
        {
            if (!CanSpool)
                return false;

            SpoolDuration = EstimatedSpoolTime;
            spoolStartTime = Time.time;
            IsSpooling = true;
            SpoolStarted?.Invoke();
            return true;
        }

        public void CancelSpool()
        {
            if (!IsSpooling)
                return;

            IsSpooling = false;
            SpoolCancelled?.Invoke();
        }

        public void ToggleSpool()
        {
            if (IsSpooling)
                CancelSpool();
            else
                StartSpool();
        }

        /// <summary>Finishes the spool now, starting one first if needed. For debug tools.</summary>
        public void CompleteSpool()
        {
            if (IsSpooling || StartSpool())
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
            if (vehicle == null)
                return;

            vehicle.onExitedAll.RemoveListener(CancelSpool);
            vehicle.onDestroyed.RemoveListener(CancelSpool);
        }

        private void OnDisable()
        {
            // Stops without the event: during scene unload its listeners may already be gone.
            IsSpooling = false;
        }

        private void Update()
        {
            if (!IsSpooling)
                return;

            if (heatPerSecond > 0f && HeatTracker.Active != null)
                HeatTracker.Active.Add(heatPerSecond * Time.deltaTime);

            if (Time.time - spoolStartTime >= SpoolDuration)
                Jump();
        }

        private void Jump()
        {
            IsSpooling = false;
            HasJumped = true;
            Jumped?.Invoke();
        }
    }
}
