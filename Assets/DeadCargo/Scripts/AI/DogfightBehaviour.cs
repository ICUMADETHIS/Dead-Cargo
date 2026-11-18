using UnityEngine;
using VSX.UniversalVehicleCombat;
using VSX.UniversalVehicleCombat.Radar;

namespace DeadCargo.AI
{
    /// <summary>
    /// The fight with the tracked target: attack runs (lead aim, bursts, missiles once locked), broken up by
    /// break-aways. A run lasts its time, or breaks off early inside the minimum engage distance. A break lasts its
    /// time, or turns back early beyond the maximum engage distance, so a break never carries the ship out of the
    /// fight. Run by a <see cref="PilotBrain"/>.
    /// </summary>
    public sealed class DogfightBehaviour : AIVehicleBehaviour
    {
        [Header("Manoeuvres")]
        [Tooltip("Attack runs: lead the target, fire in bursts, launch missiles once locked.")]
        [SerializeField] private AttackRunBehaviour attack;

        [Tooltip("Break-aways between attack runs.")]
        [SerializeField] private BreakAwayBehaviour breakAway;

        [Header("Range")]
        [Tooltip("An attack run breaks off when the ship comes this close to the target, in metres.")]
        [SerializeField, Min(0f)] private float minEngageDistance = 100f;

        [Tooltip("A break-away turns back for a run once the ship is this far from the target, in metres.")]
        [SerializeField, Min(0f)] private float maxEngageDistance = 800f;

        [Header("Timing")]
        [Tooltip("Length of an attack run in seconds, picked between the least (x) and the most (y).")]
        [SerializeField] private Vector2 attackTime = new Vector2(10f, 20f);

        [Tooltip("Length of a break-away in seconds, picked between the least (x) and the most (y).")]
        [SerializeField] private Vector2 breakTime = new Vector2(4f, 8f);

        private Weapons weapons;
        private float manoeuvreEnd;

        /// <summary>The half of the fight being flown now.</summary>
        public DogfightManoeuvre Manoeuvre { get; private set; }

        /// <summary>Time left in the current attack run or break-away, in seconds.</summary>
        public float ManoeuvreTimeLeft => Mathf.Max(0f, manoeuvreEnd - Time.time);

        private AIVehicleBehaviour Current => Manoeuvre == DogfightManoeuvre.Attacking ? (AIVehicleBehaviour)attack : breakAway;

        /// <summary>True while the attack run's guns would fire but a wingman is in the line of fire.</summary>
        public bool IsHoldingFire => attack != null && Manoeuvre == DogfightManoeuvre.Attacking && attack.IsHoldingFire;

        /// <summary>Sets how well the attack runs are flown and fought.</summary>
        public void SetSkill(PilotSkill skill)
        {
            if (attack != null)
                attack.SetSkill(skill);
        }

        /// <summary>Sets the wing the pilot fights with.</summary>
        public void SetWing(PilotWing wing, PilotBrain pilot)
        {
            if (attack != null)
                attack.SetWing(wing, pilot);
        }

        protected override bool Initialize(Vehicle vehicle)
        {
            if (!base.Initialize(vehicle) || attack == null || breakAway == null)
                return false;

            weapons = vehicle.GetComponent<Weapons>();
            if (weapons == null)
                return false;

            attack.SetVehicle(vehicle);
            breakAway.SetVehicle(vehicle);

            // The first engagement starts with an attack run.
            Manoeuvre = DogfightManoeuvre.Attacking;
            manoeuvreEnd = 0f;
            return attack.Initialized && breakAway.Initialized;
        }

        public override void StartBehaviour()
        {
            base.StartBehaviour();
            if (state != VehicleBehaviourState.Started)
                return;

            // Resume the manoeuvre that avoidance or a lost target interrupted, or start a fresh run once it is over.
            if (Time.time >= manoeuvreEnd)
                Begin(DogfightManoeuvre.Attacking);
            else
                Current.StartBehaviour();
        }

        public override void StopBehaviour()
        {
            base.StopBehaviour();
            if (attack != null)
                attack.StopBehaviour();
            if (breakAway != null)
                breakAway.StopBehaviour();
        }

        public override bool BehaviourUpdate()
        {
            if (!base.BehaviourUpdate())
                return false;

            TargetSelector selector = weapons.WeaponsTargetSelector;
            Trackable target = selector != null ? selector.SelectedTarget : null;
            if (target == null)
                return false;

            float distance = Vector3.Distance(vehicle.transform.position, target.transform.position);
            bool over = Time.time >= manoeuvreEnd;

            if (Manoeuvre == DogfightManoeuvre.Attacking)
            {
                if (distance < minEngageDistance || (over && distance <= maxEngageDistance))
                    Begin(DogfightManoeuvre.Breaking);
                else if (over)
                    manoeuvreEnd = Time.time + PickTime(attackTime);    // Still closing on a distant target: keep the run going.
            }
            else if (over || distance > maxEngageDistance)
            {
                Begin(DogfightManoeuvre.Attacking);
            }

            return Current.BehaviourUpdate();
        }

        private void Begin(DogfightManoeuvre manoeuvre)
        {
            AIVehicleBehaviour previous = Current;
            Manoeuvre = manoeuvre;
            manoeuvreEnd = Time.time + PickTime(manoeuvre == DogfightManoeuvre.Attacking ? attackTime : breakTime);

            if (previous != Current)
                previous.StopBehaviour();
            Current.StartBehaviour();
        }

        private static float PickTime(Vector2 range)
        {
            return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
        }
    }
}
