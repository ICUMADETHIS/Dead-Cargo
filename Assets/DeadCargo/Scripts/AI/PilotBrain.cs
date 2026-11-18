using System;
using UnityEngine;
using UnityEngine.Serialization;
using VSX.UniversalVehicleCombat;
using VSX.UniversalVehicleCombat.Radar;

namespace DeadCargo.AI
{
    /// <summary>
    /// Dead Cargo's AI pilot, in place of the kit's behaviour selector. It flies one readable <see cref="PilotState"/>
    /// at a time: it fights the target its ship's sensors track, breaks off to regroup when its skill says it is hurt,
    /// pursues to where it last tracked a target, circles there, and gives up after a while; once ordered to withdraw
    /// it flies away for good. Every state is flown through <see cref="PilotFlight"/>, whose collision avoidance bends
    /// the course in all of them. It never falls back to the world's origin. Lives on the pilot's game agent, which
    /// hands it each vehicle the pilot enters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PilotBrain : VehicleInput
    {
        // How far round the circle ahead of the ship the circling goal sits, in degrees.
        private const float HoldLeadAngle = 35f;

        // Straight-line goals sit this far out, so they never come within reach.
        private const float FarGoalDistance = 2000f;

        // Avoidance this strong counts as avoiding, for readouts.
        private const float AvoidingWeight = 0.5f;

        // Largest angle a regroup heads off the line straight away from the target, in degrees.
        private const float RegroupCone = 40f;

        [Header("Flight")]
        [Tooltip("Handles the ship: turning, speed, thrusters and collision avoidance.")]
        [SerializeField] private PilotFlight pilotFlight;

        [Header("Behaviours")]
        [Tooltip("Attack runs and break-aways against the tracked target.")]
        [SerializeField] private DogfightBehaviour dogfight;

        [Tooltip("Flies pursuit, holding, regrouping and withdrawal.")]
        [FormerlySerializedAs("flight")]
        [SerializeField] private FlyToBehaviour flyTo;

        [Header("Pursuit")]
        [Tooltip("A lost target is looked for where it was last tracked, led by its motion for at most this long, in seconds.")]
        [SerializeField, Min(0f)] private float pursuitLead = 4f;

        [Tooltip("The pursuit ends this close to where the target should be, and the pilot circles there, in metres.")]
        [SerializeField, Min(10f)] private float arrivalDistance = 150f;

        [Tooltip("Speed while pursuing, as a share of top speed.")]
        [FormerlySerializedAs("pursuitThrottle")]
        [SerializeField, Range(0f, 1f)] private float pursuitSpeed = 1f;

        [Tooltip("The pursuit boosts while further than this from where the target should be, in metres.")]
        [SerializeField, Min(0f)] private float pursuitBoostDistance = 800f;

        [Header("Holding")]
        [Tooltip("Radius of the circle flown while holding or regrouping, in metres.")]
        [SerializeField, Min(20f)] private float holdRadius = 250f;

        [Tooltip("Speed while circling, as a share of top speed.")]
        [FormerlySerializedAs("holdThrottle")]
        [SerializeField, Range(0f, 1f)] private float holdSpeed = 0.5f;

        [Tooltip("Time spent holding with no target before the pilot gives up, in seconds. Whoever sent it decides what happens then.")]
        [SerializeField, Min(0f)] private float giveUpTime = 20f;

        [Header("Regrouping")]
        [Tooltip("The ship's shield health type. With it the pilot knows when its shields are failing.")]
        [SerializeField] private HealthType shieldType;

        [Tooltip("The ship's hull health type.")]
        [SerializeField] private HealthType hullType;

        [Tooltip("A regrouping pilot flies this far from its target, then circles, in metres.")]
        [SerializeField, Min(100f)] private float regroupDistance = 1200f;

        [Tooltip("A regrouping pilot comes back once its shields are back to this share, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float returnShield = 0.5f;

        [Tooltip("Least time a regroup lasts, in seconds.")]
        [SerializeField, Min(0f)] private float shortestRegroup = 5f;

        [Tooltip("Most time a regroup lasts, in seconds, recharged or not.")]
        [SerializeField, Min(0f)] private float longestRegroup = 25f;

        [Tooltip("Time after a regroup before the pilot will break off again, in seconds.")]
        [SerializeField, Min(0f)] private float regroupCooldown = 15f;

        [Header("Withdrawal")]
        [Tooltip("Boost while withdrawing, for as long as the ship's boost lasts.")]
        [SerializeField] private bool boostWhileWithdrawing = true;

        private Weapons weapons;
        private Health health;
        private bool hasShield;
        private bool hasHull;
        private AIVehicleBehaviour activeBehaviour;
        private PilotSkill skill = new PilotSkill();

        private bool hasLastKnown;
        private Vector3 lastKnownPosition;
        private Vector3 lastKnownVelocity;
        private float lastKnownTime;

        private Vector3 holdCentre;
        private Vector3 holdNormal;
        private float holdStartTime;
        private bool gaveUp;

        private bool isRegrouping;
        private bool regroupCircling;
        private Vector3 regroupHeading;
        private Vector3 regroupCentre;
        private Vector3 regroupNormal;
        private float regroupStartTime;
        private float nextRegroupTime;
        private bool hullBreakUsed;

        private bool isWithdrawing;
        private Vector3 withdrawHeading;

        /// <summary>Raised with the new state whenever it changes.</summary>
        public event Action<PilotState> StateChanged;

        /// <summary>Raised once per hold when the pilot has circled for the give-up time without finding a target.</summary>
        public event Action GaveUp;

        public PilotState State { get; private set; } = PilotState.Holding;

        /// <summary>The ship this pilot flies, or null.</summary>
        public Vehicle Vehicle { get; private set; }

        /// <summary>The skill asset the pilot was given, or null for the Regular default.</summary>
        public PilotSkillDefinition SkillDefinition { get; private set; }

        /// <summary>How well the pilot flies and fights.</summary>
        public PilotSkill Skill => skill;

        public string SkillName => SkillDefinition != null ? SkillDefinition.DisplayName : "Regular";

        /// <summary>The wing the pilot fights with, or null.</summary>
        public PilotWing Wing { get; private set; }

        /// <summary>The target the ship's sensors track now, or null.</summary>
        public Trackable Target
        {
            get
            {
                TargetSelector selector = weapons != null ? weapons.WeaponsTargetSelector : null;
                return selector != null ? selector.SelectedTarget : null;
            }
        }

        public bool HasLastKnownPosition => hasLastKnown;

        /// <summary>Where a target was last tracked. World space; valid while <see cref="HasLastKnownPosition"/>.</summary>
        public Vector3 LastKnownPosition => lastKnownPosition;

        /// <summary>Seconds since a target was last tracked; infinite when none ever was.</summary>
        public float TimeSinceTargetSeen => hasLastKnown ? Time.time - lastKnownTime : float.PositiveInfinity;

        /// <summary>The half of the dogfight being flown. Meaningful while engaging.</summary>
        public DogfightManoeuvre Manoeuvre => dogfight != null ? dogfight.Manoeuvre : DogfightManoeuvre.Attacking;

        /// <summary>True while the guns would fire but a wingman is in the line of fire.</summary>
        public bool IsHoldingFire => State == PilotState.Engaging && dogfight != null && dogfight.IsHoldingFire;

        /// <summary>Where the ship is flying while pursuing, holding, regrouping or withdrawing. World space.</summary>
        public Vector3 FlightGoal => flyTo != null ? flyTo.Goal : Vector3.zero;

        /// <summary>The heading the ship is turning to, after avoidance. World space, unit length.</summary>
        public Vector3 Heading => pilotFlight != null ? pilotFlight.Heading : Vector3.forward;

        /// <summary>True while collision avoidance has most of the course.</summary>
        public bool IsAvoiding => pilotFlight != null && pilotFlight.AvoidanceWeight >= AvoidingWeight;

        public bool IsBoosting => pilotFlight != null && pilotFlight.IsBoosting;

        /// <summary>Seconds of holding left before the pilot gives up. Zero in other states.</summary>
        public float HoldTimeLeft => State == PilotState.Holding ? Mathf.Max(0f, holdStartTime + giveUpTime - Time.time) : 0f;

        /// <summary>The ship's shields, 0 to 1, or -1 when the pilot cannot read them.</summary>
        public float ShieldShare => hasShield ? health.GetCurrentHealthFractionByType(shieldType) : -1f;

        /// <summary>The ship's hull, 0 to 1, or -1 when the pilot cannot read it.</summary>
        public float HullShare => hasHull ? health.GetCurrentHealthFractionByType(hullType) : -1f;

        public bool IsWithdrawing => isWithdrawing;

        /// <summary>Sets how well the pilot flies and fights. Null sets the Regular skill.</summary>
        public void SetSkill(PilotSkillDefinition definition)
        {
            SkillDefinition = definition;
            skill = definition != null ? definition.Skill : new PilotSkill();
            ApplySkill();
        }

        /// <summary>Sets the wing the pilot fights with: it fans out from its wingmen and holds fire when one is in the way.</summary>
        public void JoinWing(PilotWing wing)
        {
            Wing = wing;
            if (dogfight != null)
                dogfight.SetWing(wing, this);
        }

        /// <summary>Takes the pilot off the hunt for good: it flies straight away from the given point. It still avoids collisions.</summary>
        public void Withdraw(Vector3 awayFrom)
        {
            if (isWithdrawing)
                return;

            isWithdrawing = true;
            Transform ship = Vehicle != null ? Vehicle.transform : transform;
            Vector3 away = ship.position - awayFrom;
            withdrawHeading = away.sqrMagnitude > 1f ? away.normalized : ship.forward;
        }

        /// <summary>Knocks the ship's shields out, as if shot away; the ship's recharger restores them as usual. For debug tools. Returns false without shields.</summary>
        public bool BreakShields()
        {
            if (!hasShield)
                return false;

            foreach (Damageable damageable in health.Damageables)
            {
                if (damageable.HealthType == shieldType && !damageable.Destroyed)
                    damageable.Destroy();
            }

            return true;
        }

        public override void SetVehicle(Vehicle vehicle)
        {
            StopActiveBehaviour();
            if (pilotFlight != null)
                pilotFlight.Release();

            Vehicle = null;
            base.SetVehicle(vehicle);
        }

        protected override bool Initialize(Vehicle vehicle)
        {
            if (!base.Initialize(vehicle))
                return false;

            weapons = vehicle.GetComponent<Weapons>();
            if (weapons == null || pilotFlight == null || dogfight == null || flyTo == null || !pilotFlight.Bind(vehicle))
            {
                Debug.LogWarning($"{name} cannot fly {vehicle.name}: it needs Weapons and 3D engines on the ship, and its flight, dogfight and fly-to parts assigned.", this);
                return false;
            }

            dogfight.SetVehicle(vehicle);
            flyTo.SetVehicle(vehicle);
            if (!dogfight.Initialized || !flyTo.Initialized)
                return false;

            health = vehicle.GetComponent<Health>();
            hasShield = health != null && shieldType != null && health.GetMaxHealthByType(shieldType) > 0f;
            hasHull = health != null && hullType != null && health.GetMaxHealthByType(hullType) > 0f;

            Vehicle = vehicle;
            hasLastKnown = false;
            isRegrouping = false;
            hullBreakUsed = false;
            nextRegroupTime = 0f;
            isWithdrawing = false;
            State = PilotState.Holding;
            ApplySkill();
            BeginHold(vehicle.transform.position);
            return true;
        }

        protected override void InputUpdate()
        {
            if (Vehicle == null)
                return;

            if (Vehicle.Destroyed)
            {
                StopActiveBehaviour();
                pilotFlight.Release();
                return;
            }

            Trackable target = isWithdrawing ? null : Target;
            if (target != null)
                Remember(target);

            if (isRegrouping && IsRegroupOver())
                EndRegroup();

            if (isWithdrawing)
            {
                Vector3 position = Vehicle.transform.position;
                Fly(PilotState.Withdrawing, position + withdrawHeading * FarGoalDistance, 1f, boostWhileWithdrawing);
            }
            else if (isRegrouping || TryBreakOff())
            {
                Regroup(target);
            }
            else if (target != null)
            {
                Enter(PilotState.Engaging, dogfight);
                dogfight.BehaviourUpdate();
            }
            else if (hasLastKnown && lastKnownTime > holdStartTime)
            {
                Pursue();
            }
            else
            {
                Hold();
            }
        }

        private void ApplySkill()
        {
            if (pilotFlight != null)
                pilotFlight.SetSteeringShare(skill.SteeringShare);
            if (dogfight != null)
                dogfight.SetSkill(skill);
        }

        private void Remember(Trackable target)
        {
            hasLastKnown = true;
            lastKnownPosition = target.transform.position;
            lastKnownVelocity = target.Velocity;
            lastKnownTime = Time.time;
        }

        // Heads for where the target should be by now. On arrival the pilot circles there, which ends the pursuit:
        // a pursuit runs only while the last sighting is newer than the hold.
        private void Pursue()
        {
            Vector3 goal = PilotMath.PredictedPosition(lastKnownPosition, lastKnownVelocity, Time.time - lastKnownTime, pursuitLead);
            float sqrDistance = (goal - Vehicle.transform.position).sqrMagnitude;
            if (sqrDistance <= arrivalDistance * arrivalDistance)
            {
                BeginHold(goal);
                Hold();
                return;
            }

            Fly(PilotState.Pursuing, goal, pursuitSpeed, sqrDistance > pursuitBoostDistance * pursuitBoostDistance);
        }

        private void Hold()
        {
            Vector3 goal = PilotMath.OrbitGoal(holdCentre, holdNormal, Vehicle.transform.position, holdRadius, HoldLeadAngle);
            Fly(PilotState.Holding, goal, holdSpeed, false);

            if (!gaveUp && Time.time >= holdStartTime + giveUpTime)
            {
                gaveUp = true;
                GaveUp?.Invoke();
            }
        }

        // Circles in the plane the ship is flying in when the hold starts, so it rolls into the circle, not over.
        private void BeginHold(Vector3 centre)
        {
            holdCentre = centre;
            holdNormal = Vehicle.transform.up;
            holdStartTime = Time.time;
            gaveUp = false;
        }

        // Starts a regroup when the pilot, fighting, is hurt by its skill's measure: shields at or under its share, or, once, the hull.
        private bool TryBreakOff()
        {
            if (State != PilotState.Engaging || Time.time < nextRegroupTime)
                return false;

            bool shieldsFailing = hasShield && skill.BreakOffShield > 0f && ShieldShare <= skill.BreakOffShield;
            bool hullFailing = hasHull && !hullBreakUsed && skill.BreakOffHull > 0f && HullShare <= skill.BreakOffHull;
            if (!shieldsFailing && !hullFailing)
                return false;

            if (hullFailing)
                hullBreakUsed = true;

            isRegrouping = true;
            regroupCircling = false;
            regroupStartTime = Time.time;
            Vector3 from = hasLastKnown ? lastKnownPosition : Vehicle.transform.position - Vehicle.transform.forward;
            regroupHeading = PilotMath.BreakHeading(Vehicle.transform.position - from, UnityEngine.Random.Range(0f, RegroupCone), UnityEngine.Random.value);
            return true;
        }

        // Boosts away from the target, then circles out of the fight until the shields recover or time runs out.
        private void Regroup(Trackable target)
        {
            Vector3 position = Vehicle.transform.position;
            if (!regroupCircling)
            {
                Vector3 threat = target != null ? target.transform.position : lastKnownPosition;
                if ((position - threat).sqrMagnitude >= regroupDistance * regroupDistance)
                {
                    regroupCircling = true;
                    regroupCentre = position;
                    regroupNormal = Vehicle.transform.up;
                }
            }

            if (regroupCircling)
                Fly(PilotState.Regrouping, PilotMath.OrbitGoal(regroupCentre, regroupNormal, position, holdRadius, HoldLeadAngle), holdSpeed, false);
            else
                Fly(PilotState.Regrouping, position + regroupHeading * FarGoalDistance, 1f, true);
        }

        private bool IsRegroupOver()
        {
            float elapsed = Time.time - regroupStartTime;
            if (elapsed >= longestRegroup)
                return true;

            return elapsed >= shortestRegroup && (!hasShield || ShieldShare >= returnShield);
        }

        private void EndRegroup()
        {
            isRegrouping = false;
            nextRegroupTime = Time.time + regroupCooldown;
        }

        private void Fly(PilotState next, Vector3 goal, float speed, bool boost)
        {
            flyTo.SetGoal(goal, speed, boost);
            Enter(next, flyTo);
            flyTo.BehaviourUpdate();
        }

        // Switches state; the behaviour that flew the last state stops (ceasing fire) before the next starts.
        private void Enter(PilotState next, AIVehicleBehaviour behaviour)
        {
            if (behaviour != activeBehaviour)
            {
                StopActiveBehaviour();
                activeBehaviour = behaviour;
                activeBehaviour.StartBehaviour();
            }

            if (next == State)
                return;

            State = next;
            StateChanged?.Invoke(next);
        }

        private void StopActiveBehaviour()
        {
            if (activeBehaviour != null)
                activeBehaviour.StopBehaviour();
            activeBehaviour = null;
        }
    }
}
