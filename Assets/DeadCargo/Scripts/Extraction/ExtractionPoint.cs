using System;
using System.Collections.Generic;
using DeadCargo.Heat;
using UnityEngine;

namespace DeadCargo.Extraction
{
    /// <summary>
    /// A fixed way out of the sector. Dormant until the player starts its uplink, which takes time and raises heat.
    /// It then opens for a limited window, during which flying the run ship into it extracts the run.
    /// When the window ends it closes and can be uplinked again.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExtractionPoint : MonoBehaviour
    {
        private const float PulseSpeed = 6f;
        private const float DormantGlow = 0.6f;
        private const float ActiveGlow = 1.8f;
        private const float BaseTint = 0.35f;

        private static readonly List<ExtractionPoint> points = new List<ExtractionPoint>();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [Header("Area")]
        [Tooltip("Radius of the ring the run ship must fly into while the point is open, in metres.")]
        [SerializeField, Min(1f)] private float entryRadius = 25f;

        [Tooltip("Distance within which the player can start the uplink, in metres.")]
        [SerializeField, Min(1f)] private float activationRange = 150f;

        [Header("Timing")]
        [Tooltip("Time from starting the uplink to the point opening, in seconds.")]
        [SerializeField, Min(0f)] private float uplinkDuration = 45f;

        [Tooltip("Time the point stays open before it closes, in seconds.")]
        [SerializeField, Min(1f)] private float openDuration = 60f;

        [Header("Heat")]
        [Tooltip("Heat added when the uplink starts, because it broadcasts the player's position.")]
        [SerializeField, Min(0f)] private float uplinkHeatBurst = 15f;

        [Tooltip("Heat added per second while the uplink runs.")]
        [SerializeField, Min(0f)] private float uplinkHeatPerSecond = 0.5f;

        [Header("Visuals")]
        [Tooltip("Renderers tinted to show the state.")]
        [SerializeField] private Renderer[] stateRenderers = Array.Empty<Renderer>();

        [Tooltip("Shown only while the point is open.")]
        [SerializeField] private GameObject openBeam;

        [Tooltip("Light tinted to show the state.")]
        [SerializeField] private Light beacon;

        [SerializeField] private Color dormantColor = new Color(0.45f, 0.6f, 0.7f);
        [SerializeField] private Color uplinkColor = new Color(1f, 0.68f, 0.25f);
        [SerializeField] private Color openColor = new Color(0.45f, 1f, 0.6f);

        private MaterialPropertyBlock block;
        private float beaconIntensity;
        private float stateElapsed;

        /// <summary>Raised by any extraction point when its state changes. Static, so a HUD hears every point.</summary>
        public static event Action<ExtractionPoint> StateChanged;

        /// <summary>Every enabled extraction point in the loaded scenes.</summary>
        public static IReadOnlyList<ExtractionPoint> All => points;

        public ExtractionPointState State { get; private set; }
        public float EntryRadius => entryRadius;
        public float ActivationRange => activationRange;

        /// <summary>Time left in the uplink or the open window, in seconds. Zero while dormant.</summary>
        public float TimeRemaining
        {
            get
            {
                switch (State)
                {
                    case ExtractionPointState.Uplinking:
                        return Mathf.Max(0f, uplinkDuration - stateElapsed);
                    case ExtractionPointState.Open:
                        return Mathf.Max(0f, openDuration - stateElapsed);
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>Uplink progress while uplinking, or the share of the open window left while open. 0 to 1.</summary>
        public float Progress
        {
            get
            {
                switch (State)
                {
                    case ExtractionPointState.Uplinking:
                        return uplinkDuration > 0f ? Mathf.Clamp01(stateElapsed / uplinkDuration) : 1f;
                    case ExtractionPointState.Open:
                        return 1f - Mathf.Clamp01(stateElapsed / openDuration);
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>True when the uplink can start: the point is dormant and the run is still going.</summary>
        public bool CanStartUplink => State == ExtractionPointState.Dormant && (RunTracker.Active == null || !RunTracker.Active.IsOver);

        /// <summary>Returns the point closest to a position, or null when there are none.</summary>
        public static ExtractionPoint FindNearest(Vector3 position)
        {
            ExtractionPoint nearest = null;
            float best = float.PositiveInfinity;
            foreach (ExtractionPoint point in points)
            {
                float distance = (point.transform.position - position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = point;
                }
            }

            return nearest;
        }

        public float DistanceTo(Vector3 position)
        {
            return Vector3.Distance(transform.position, position);
        }

        public bool IsInActivationRange(Vector3 position)
        {
            return (transform.position - position).sqrMagnitude <= activationRange * activationRange;
        }

        /// <summary>Starts the uplink. The player's position is broadcast, raising heat.</summary>
        public bool StartUplink()
        {
            if (!CanStartUplink)
                return false;

            SetState(ExtractionPointState.Uplinking);
            if (HeatTracker.Active != null)
                HeatTracker.Active.Add(uplinkHeatBurst);

            return true;
        }

        /// <summary>Opens the point at once, skipping the uplink. For debug tools.</summary>
        public void OpenNow()
        {
            if (State != ExtractionPointState.Open)
                SetState(ExtractionPointState.Open);
        }

        /// <summary>Closes the point at once. For debug tools.</summary>
        public void Close()
        {
            if (State != ExtractionPointState.Dormant)
                SetState(ExtractionPointState.Dormant);
        }

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            points.Clear();
            StateChanged = null;
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            beaconIntensity = beacon != null ? beacon.intensity : 0f;
            ApplyVisuals(1f);
        }

        private void OnEnable()
        {
            points.Add(this);
        }

        private void OnDisable()
        {
            points.Remove(this);
        }

        private void Update()
        {
            if (State == ExtractionPointState.Dormant)
                return;

            stateElapsed += Time.deltaTime;

            if (State == ExtractionPointState.Uplinking)
            {
                if (uplinkHeatPerSecond > 0f && HeatTracker.Active != null)
                    HeatTracker.Active.Add(uplinkHeatPerSecond * Time.deltaTime);

                if (stateElapsed >= uplinkDuration)
                    SetState(ExtractionPointState.Open);
                else
                    ApplyVisuals(0.6f + 0.4f * Mathf.Sin(stateElapsed * PulseSpeed));

                return;
            }

            if (TryExtract())
                return;

            if (stateElapsed >= openDuration)
                SetState(ExtractionPointState.Dormant);
        }

        // The run ship must be inside the ring with the player aboard.
        private bool TryExtract()
        {
            RunTracker run = RunTracker.Active;
            if (run == null || run.IsOver || !run.IsPlayerAboard)
                return false;

            if ((run.Ship.transform.position - transform.position).sqrMagnitude > entryRadius * entryRadius)
                return false;

            return run.ExtractAtPoint();
        }

        private void SetState(ExtractionPointState state)
        {
            State = state;
            stateElapsed = 0f;
            ApplyVisuals(1f);
            StateChanged?.Invoke(this);
        }

        private void ApplyVisuals(float pulse)
        {
            Color color = State == ExtractionPointState.Open ? openColor
                : State == ExtractionPointState.Uplinking ? uplinkColor
                : dormantColor;
            Color glow = color * ((State == ExtractionPointState.Dormant ? DormantGlow : ActiveGlow) * pulse);

            foreach (Renderer stateRenderer in stateRenderers)
            {
                if (stateRenderer == null)
                    continue;

                stateRenderer.GetPropertyBlock(block);
                block.SetColor(ColorId, color * BaseTint);
                block.SetColor(EmissionId, glow);
                stateRenderer.SetPropertyBlock(block);
            }

            if (openBeam != null)
                openBeam.SetActive(State == ExtractionPointState.Open);

            if (beacon != null)
            {
                beacon.color = color;
                beacon.intensity = beaconIntensity * pulse;
            }
        }
    }
}
