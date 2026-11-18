using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.Heat
{
    /// <summary>
    /// Tracks how much attention the player has drawn during a run.
    /// Heat rises from player actions, decays after a quiet period, and is grouped into tiers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeatTracker : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHeat = 100f;

        [Tooltip("Time without new heat before decay starts, in seconds.")]
        [SerializeField, Min(0f)] private float decayDelay = 6f;

        [Tooltip("Heat lost per second while decaying.")]
        [SerializeField, Min(0f)] private float decayPerSecond = 2f;

        [Tooltip("Ordered from lowest threshold to highest. The first tier should start at 0.")]
        [SerializeField] private HeatTier[] tiers =
        {
            new HeatTier("CLEAR", 0f, new Color(0.55f, 0.85f, 0.6f)),
            new HeatTier("NOTICED", 25f, new Color(1f, 0.85f, 0.3f)),
            new HeatTier("WANTED", 50f, new Color(1f, 0.55f, 0.2f)),
            new HeatTier("HUNTED", 75f, new Color(1f, 0.3f, 0.25f))
        };

        private float lastGainTime;

        /// <summary>The tracker for the loaded scene, or null.</summary>
        public static HeatTracker Active { get; private set; }

        /// <summary>Raised whenever heat changes.</summary>
        public event Action Changed;

        /// <summary>Raised with the new tier index when heat crosses a tier threshold.</summary>
        public event Action<int> TierChanged;

        public float Heat { get; private set; }

        /// <summary>True while heat is held: it does not decay. Set by test tools through <see cref="HoldDecay"/>.</summary>
        public bool IsDecayHeld { get; private set; }

        public float MaxHeat => maxHeat;
        public float NormalizedHeat => Heat / maxHeat;
        public int TierIndex { get; private set; }
        public HeatTier CurrentTier => tiers[TierIndex];
        public IReadOnlyList<HeatTier> Tiers => tiers;

        public void Add(float amount)
        {
            if (amount <= 0f)
                return;

            lastGainTime = Time.time;
            SetHeat(Heat + amount);
        }

        /// <summary>Sets heat directly. For debug tools and scripted events.</summary>
        public void Set(float value)
        {
            lastGainTime = Time.time;
            SetHeat(value);
        }

        public void Clear()
        {
            SetHeat(0f);
        }

        /// <summary>Stops heat decaying while held, so a tier stays put. For debug tools; heat can still be raised or set.</summary>
        public void HoldDecay(bool hold)
        {
            IsDecayHeld = hold;
        }

        private void Awake()
        {
            if (tiers == null || tiers.Length == 0)
                tiers = new[] { new HeatTier("CLEAR", 0f, Color.white) };
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

        private void Update()
        {
            if (Heat > 0f && !IsDecayHeld && Time.time >= lastGainTime + decayDelay)
                SetHeat(Heat - decayPerSecond * Time.deltaTime);
        }

        private void OnValidate()
        {
            if (tiers != null)
                Array.Sort(tiers, (a, b) => a.Threshold.CompareTo(b.Threshold));
        }

        private void SetHeat(float value)
        {
            value = Mathf.Clamp(value, 0f, maxHeat);
            if (Mathf.Approximately(value, Heat))
                return;

            Heat = value;

            int previousTier = TierIndex;
            TierIndex = FindTier(Heat);

            Changed?.Invoke();
            if (TierIndex != previousTier)
                TierChanged?.Invoke(TierIndex);
        }

        // Highest tier whose threshold has been reached.
        private int FindTier(float heat)
        {
            int index = 0;
            for (int i = 1; i < tiers.Length; ++i)
            {
                if (heat >= tiers[i].Threshold)
                    index = i;
            }

            return index;
        }
    }
}
