using System;
using UnityEngine;

namespace DeadCargo.Authority
{
    /// <summary>How hard the Authority responds while heat sits in one tier.</summary>
    [Serializable]
    public struct AuthorityTierResponse
    {
        [Tooltip("Most Authority ships allowed in the field at once.")]
        [SerializeField, Min(0)] private int maxShips;

        [Tooltip("Time between reinforcements, in seconds.")]
        [SerializeField, Min(0.5f)] private float spawnInterval;

        public AuthorityTierResponse(int maxShips, float spawnInterval)
        {
            this.maxShips = maxShips;
            this.spawnInterval = spawnInterval;
        }

        public int MaxShips => maxShips;
        public float SpawnInterval => spawnInterval;
    }
}
