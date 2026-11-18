using UnityEngine;
using VSX.UniversalVehicleCombat;
using VSX.UniversalVehicleCombat.Radar;

namespace DeadCargo.AI
{
    /// <summary>
    /// An attack run at the tracked target, flown through <see cref="PilotFlight"/> and as well as the pilot's
    /// <see cref="PilotSkill"/> allows. The aim follows the target's lead position (where shots meet it) with the
    /// pilot's reaction time and wanders off it by its aim error; the guns fire in bursts while the aim is in the
    /// sights, and missiles launch once locked. Far off, the run boosts to close, and wingmen fan out to come at the
    /// target from different sides; near, it matches the target's speed and settles on its tail at gun range. The guns
    /// hold fire while a wingman is in the line of fire. Run by a <see cref="DogfightBehaviour"/>.
    /// </summary>
    public sealed class AttackRunBehaviour : AIVehicleBehaviour
    {
        // How hard the run closes on its tail distance: metres a second of closing per metre off it.
        private const float TailGain = 0.5f;

        // The slowest a run flies, as a share of top speed, so a target coming head-on is still met at speed.
        private const float SlowestSpeed = 0.35f;

        // How fast the aim error wanders.
        private const float AimWanderRate = 0.8f;

        [Tooltip("Flies the ship.")]
        [SerializeField] private PilotFlight pilotFlight;

        [Header("Approach")]
        [Tooltip("Distance the run settles at behind a target, in metres.")]
        [SerializeField, Min(20f)] private float tailDistance = 250f;

        [Tooltip("Beyond this distance from the target the run boosts to close, in metres.")]
        [SerializeField, Min(0f)] private float boostDistance = 900f;

        [Tooltip("Most speed added to or taken from the target's while settling on its tail, in metres a second.")]
        [SerializeField, Min(0f)] private float closingSpeed = 40f;

        [Header("Wing")]
        [Tooltip("Beyond this distance from the target, wingmen fan out to come at it from different sides, in metres.")]
        [SerializeField, Min(0f)] private float spreadDistance = 450f;

        [Tooltip("Wingmen attacking from bearings closer than this push each other apart, in degrees.")]
        [SerializeField, Range(0f, 180f)] private float spreadAngle = 60f;

        [Tooltip("The guns hold fire while a wingman is within this distance of the line of fire, in metres.")]
        [SerializeField, Min(0f)] private float wingmanClearance = 30f;

        private Weapons weapons;
        private TriggerablesManager triggers;
        private PilotSkill skill = new PilotSkill();
        private PilotWing wing;
        private PilotBrain pilot;
        private Vector3 aimPoint;
        private float wanderSeed;
        private bool firing;
        private float gunSwitchTime;
        private float nextMissileTime;

        /// <summary>True while the guns are firing.</summary>
        public bool IsFiring => firing;

        /// <summary>True while the guns would fire but a wingman is in the line of fire.</summary>
        public bool IsHoldingFire { get; private set; }

        /// <summary>Sets how well the run is flown and fought. Null sets the Regular skill.</summary>
        public void SetSkill(PilotSkill newSkill)
        {
            skill = newSkill ?? new PilotSkill();
        }

        /// <summary>Sets the wing the pilot attacks with, so it fans out from its wingmen and holds fire when one is in the way.</summary>
        public void SetWing(PilotWing newWing, PilotBrain owner)
        {
            wing = newWing;
            pilot = owner;
        }

        protected override bool Initialize(Vehicle vehicle)
        {
            if (!base.Initialize(vehicle) || pilotFlight == null)
                return false;

            weapons = vehicle.GetComponent<Weapons>();
            triggers = vehicle.GetComponent<TriggerablesManager>();
            return weapons != null && triggers != null;
        }

        public override void StartBehaviour()
        {
            base.StartBehaviour();
            if (state != VehicleBehaviourState.Started)
                return;

            // A fresh run starts with the aim on the target; reactions only slow how it follows from there.
            Trackable target = CurrentTarget();
            aimPoint = target != null ? target.transform.position : vehicle.transform.position + vehicle.transform.forward * 100f;
            wanderSeed = Random.Range(0f, 100f);
            firing = false;
            IsHoldingFire = false;
            gunSwitchTime = Time.time;
            nextMissileTime = Time.time + PickTime(skill.MissileInterval);
        }

        public override void StopBehaviour()
        {
            base.StopBehaviour();
            if (initialized)
                triggers.StopTriggeringAll();
            firing = false;
            IsHoldingFire = false;
        }

        public override bool BehaviourUpdate()
        {
            if (!base.BehaviourUpdate())
                return false;

            Trackable target = CurrentTarget();
            if (target == null)
                return false;

            Transform ship = vehicle.transform;
            Vector3 targetPosition = target.transform.position;
            Vector3 targetVelocity = target.Velocity;
            Vector3 aim = Aim(ship.position, weapons.GetAverageLeadTargetPosition(targetPosition, targetVelocity));

            Vector3 toTarget = targetPosition - ship.position;
            float distance = toTarget.magnitude;
            bool far = distance > boostDistance;

            float speedShare = 1f;
            if (!far && pilotFlight.TopSpeed > 0f)
            {
                float along = distance > 1e-3f ? Vector3.Dot(targetVelocity, toTarget / distance) : 0f;
                float speed = PilotMath.TailSpeed(distance, tailDistance, along, closingSpeed, TailGain);
                speedShare = Mathf.Clamp(speed / pilotFlight.TopSpeed, SlowestSpeed, 1f);
            }

            pilotFlight.FlyTo(SteerPoint(aim, targetPosition, distance), speedShare, far);

            Vector3 toAim = aim - ship.position;
            bool inSights = Vector3.Angle(ship.forward, toAim) < skill.FireCone && toAim.magnitude < skill.FiringRange;
            IsHoldingFire = inSights && wing != null && wing.IsLineBlocked(pilot, ship.position, targetPosition, wingmanClearance);
            UpdateGuns(inSights && !IsHoldingFire);
            UpdateMissiles();
            return true;
        }

        // Where the pilot thinks its shots should go: the lead, followed with its reaction time, wandering by its aim error.
        private Vector3 Aim(Vector3 shipPosition, Vector3 lead)
        {
            aimPoint = PilotMath.Follow(aimPoint, lead, skill.ReactionTime, Time.deltaTime);

            Vector3 toAim = aimPoint - shipPosition;
            PilotMath.Perpendiculars(toAim, out Vector3 first, out Vector3 second);
            float time = Time.time * AimWanderRate;
            float sideways = (Mathf.PerlinNoise(wanderSeed + time, 0f) - 0.5f) * 2f;
            float upways = (Mathf.PerlinNoise(0f, wanderSeed + time) - 0.5f) * 2f;
            return shipPosition + PilotMath.Deflect(toAim, first, second, sideways, upways, skill.AimError) * toAim.magnitude;
        }

        // Far off, a wingman fans out to its own side of the target first; near, or alone, it flies at its aim.
        private Vector3 SteerPoint(Vector3 aim, Vector3 targetPosition, float distance)
        {
            if (wing == null || pilot == null || distance <= spreadDistance || distance < 1e-3f)
                return aim;

            Vector3 bearing = wing.SpreadBearing(pilot, targetPosition, spreadAngle);
            Vector3 own = (vehicle.transform.position - targetPosition) / distance;
            return Vector3.Angle(own, bearing) > 1f ? targetPosition + bearing * spreadDistance : aim;
        }

        // Bursts and pauses alternate while the aim is in the sights. The guns stop the moment it leaves them,
        // and a pause follows before the next burst.
        private void UpdateGuns(bool canFire)
        {
            if (!canFire)
            {
                if (firing)
                    SetFiring(false);
                return;
            }

            if (Time.time >= gunSwitchTime)
                SetFiring(!firing);
        }

        private void SetFiring(bool fire)
        {
            firing = fire;
            if (fire)
            {
                triggers.StartTriggeringAtIndex(0);
                gunSwitchTime = Time.time + PickTime(skill.BurstTime);
            }
            else
            {
                triggers.StopTriggeringAtIndex(0);
                gunSwitchTime = Time.time + PickTime(skill.BurstPause);
            }
        }

        private void UpdateMissiles()
        {
            if (!skill.UsesMissiles || Time.time < nextMissileTime || weapons.MissileWeapons.Count == 0)
                return;

            TargetLocker locker = weapons.MissileWeapons[0].GetComponent<TargetLocker>();
            if (locker == null || locker.LockState != LockState.Locked)
                return;

            triggers.TriggerOnce(1);
            nextMissileTime = Time.time + PickTime(skill.MissileInterval);
        }

        private Trackable CurrentTarget()
        {
            TargetSelector selector = weapons != null ? weapons.WeaponsTargetSelector : null;
            return selector != null ? selector.SelectedTarget : null;
        }

        private static float PickTime(Vector2 range)
        {
            return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
        }
    }
}
