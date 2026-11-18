using System;
using DeadCargo.AI;
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

        [Tooltip("How well the pilots sent at this tier fly and fight. Empty sends Regular pilots. A pilot keeps its skill when heat changes.")]
        [SerializeField] private PilotSkillDefinition pilotSkill;

        public AuthorityTierResponse(int maxShips, float spawnInterval, PilotSkillDefinition pilotSkill = null)
        {
            this.maxShips = maxShips;
            this.spawnInterval = spawnInterval;
            this.pilotSkill = pilotSkill;
        }

        public int MaxShips => maxShips;
        public float SpawnInterval => spawnInterval;
        public PilotSkillDefinition PilotSkill => pilotSkill;
    }
}
