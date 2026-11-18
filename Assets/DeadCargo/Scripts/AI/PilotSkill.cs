using System;
using UnityEngine;

namespace DeadCargo.AI
{
    /// <summary>
    /// How well an AI pilot flies and fights: aim, reactions, how hard it turns, gun discipline, missiles, and when it
    /// breaks off to save itself. A new instance holds the Regular preset. The kit's guns aim themselves onto the lead
    /// within 3.3° of the nose (its aim assist), so aim error only costs hits beyond that.
    /// </summary>
    [Serializable]
    public sealed class PilotSkill
    {
        [Header("Aim")]
        [Tooltip("How far the pilot's aim wanders off the perfect lead, at most, in degrees. Past the kit's 3.3° aim assist, shots miss.")]
        [SerializeField, Range(0f, 20f)] private float aimError = 5f;

        [Tooltip("How long the aim takes to catch up with the target's moves, in seconds. Slow aim falls behind a target that jinks.")]
        [SerializeField, Range(0f, 2f)] private float reactionTime = 0.35f;

        [Tooltip("Share of the ship's steering the pilot dares to use, 0 to 1.")]
        [SerializeField, Range(0.3f, 1f)] private float steeringShare = 0.9f;

        [Header("Guns")]
        [Tooltip("The guns fire while the aim is within this angle of the nose, in degrees. Wide sprays, narrow waits for the shot.")]
        [SerializeField, Range(1f, 45f)] private float fireCone = 15f;

        [Tooltip("The guns fire only within this distance of the target, in metres.")]
        [SerializeField, Min(0f)] private float firingRange = 600f;

        [Tooltip("Length of a burst in seconds, picked between the least (x) and the most (y).")]
        [SerializeField] private Vector2 burstTime = new Vector2(1f, 3f);

        [Tooltip("Pause between bursts in seconds, picked between the least (x) and the most (y).")]
        [SerializeField] private Vector2 burstPause = new Vector2(0.5f, 2f);

        [Header("Missiles")]
        [Tooltip("Whether the pilot launches missiles once they lock.")]
        [SerializeField] private bool usesMissiles = true;

        [Tooltip("Time between missiles once locked, in seconds, picked between the least (x) and the most (y).")]
        [SerializeField] private Vector2 missileInterval = new Vector2(10f, 25f);

        [Header("Breaking Off")]
        [Tooltip("The pilot breaks off to let its shields recharge once they fall to this share, 0 to 1. 0 never breaks off.")]
        [SerializeField, Range(0f, 1f)] private float breakOffShield = 0.05f;

        [Tooltip("The pilot breaks off once, when its hull falls to this share, 0 to 1. 0 never does.")]
        [SerializeField, Range(0f, 1f)] private float breakOffHull;

        public float AimError => aimError;
        public float ReactionTime => reactionTime;
        public float SteeringShare => steeringShare;
        public float FireCone => fireCone;
        public float FiringRange => firingRange;
        public Vector2 BurstTime => burstTime;
        public Vector2 BurstPause => burstPause;
        public bool UsesMissiles => usesMissiles;
        public Vector2 MissileInterval => missileInterval;
        public float BreakOffShield => breakOffShield;
        public float BreakOffHull => breakOffHull;

        /// <summary>The Regular preset.</summary>
        public PilotSkill()
        {
        }

        private PilotSkill(float aimError, float reactionTime, float steeringShare, float fireCone, float firingRange,
            Vector2 burstTime, Vector2 burstPause, bool usesMissiles, Vector2 missileInterval, float breakOffShield, float breakOffHull)
        {
            this.aimError = aimError;
            this.reactionTime = reactionTime;
            this.steeringShare = steeringShare;
            this.fireCone = fireCone;
            this.firingRange = firingRange;
            this.burstTime = burstTime;
            this.burstPause = burstPause;
            this.usesMissiles = usesMissiles;
            this.missileInterval = missileInterval;
            this.breakOffShield = breakOffShield;
            this.breakOffHull = breakOffHull;
        }

        /// <summary>Sprays long bursts from far off with wandering, slow aim, turns gently, never uses missiles, and fights on until destroyed.</summary>
        public static PilotSkill Rookie => new PilotSkill(10f, 0.7f, 0.7f, 22f, 750f,
            new Vector2(2f, 4f), new Vector2(0.3f, 1f), false, Vector2.zero, 0f, 0f);

        /// <summary>The kit pilot's gun and missile timing, with some aim error and a little caution: breaks off when its shields go.</summary>
        public static PilotSkill Regular => new PilotSkill();

        /// <summary>Tight aim and quick reactions, full steering, short disciplined bursts at close range, frequent missiles,
        /// and breaks off before its shields fail, and once when the hull is half gone.</summary>
        public static PilotSkill Veteran => new PilotSkill(2f, 0.12f, 1f, 6f, 500f,
            new Vector2(0.6f, 1.5f), new Vector2(0.4f, 1.2f), true, new Vector2(8f, 15f), 0.3f, 0.5f);
    }
}
