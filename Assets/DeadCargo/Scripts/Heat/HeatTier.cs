using System;
using UnityEngine;

namespace DeadCargo.Heat
{
    /// <summary>A named band of heat, entered when heat reaches its threshold.</summary>
    [Serializable]
    public struct HeatTier
    {
        [SerializeField] private string label;

        [Tooltip("Heat at which this tier begins.")]
        [SerializeField, Min(0f)] private float threshold;

        [SerializeField] private Color color;

        public HeatTier(string label, float threshold, Color color)
        {
            this.label = label;
            this.threshold = threshold;
            this.color = color;
        }

        public string Label => label;
        public float Threshold => threshold;
        public Color Color => color;
    }
}
