using System;
using UnityEngine;

namespace DeadCargo.Sectors
{
    /// <summary>
    /// How far a region may change from one run to the next. Each run draws its own values from the seed, inside
    /// these limits. All zero keeps the region exactly as authored, as a clearing round the arrival point should be.
    /// </summary>
    [Serializable]
    public struct RegionVariation
    {
        [Tooltip("Largest swing round the arrival point, in degrees either way. The region keeps its distance and " +
                 "turns with the swing, so it shows the same face to the arrival point from a new direction.")]
        [SerializeField, Range(0f, 180f)] private float orbit;

        [Tooltip("Largest move of the centre along each axis, in metres either way.")]
        [SerializeField] private Vector3 drift;

        [Tooltip("Largest extra turn about the vertical axis, in degrees either way.")]
        [SerializeField, Range(0f, 180f)] private float turn;

        [Tooltip("Largest change of each radius, as a share of it. 0.2 gives 80% to 120%.")]
        [SerializeField, Range(0f, 0.5f)] private float resize;

        public RegionVariation(float orbit, Vector3 drift, float turn, float resize)
        {
            this.orbit = Mathf.Clamp(orbit, 0f, 180f);
            this.drift = Vector3.Max(drift, Vector3.zero);
            this.turn = Mathf.Clamp(turn, 0f, 180f);
            this.resize = Mathf.Clamp(resize, 0f, 0.5f);
        }

        /// <summary>Largest swing round the arrival point, in degrees either way.</summary>
        public float Orbit => orbit;

        /// <summary>Largest move of the centre along each axis, in metres either way.</summary>
        public Vector3 Drift => Vector3.Max(drift, Vector3.zero);

        /// <summary>Largest extra turn about the vertical axis, in degrees either way.</summary>
        public float Turn => turn;

        /// <summary>Largest change of each radius, as a share of it.</summary>
        public float Resize => resize;

        /// <summary>True when the region never changes.</summary>
        public bool IsFixed => orbit <= 0f && turn <= 0f && resize <= 0f && Drift == Vector3.zero;
    }
}
