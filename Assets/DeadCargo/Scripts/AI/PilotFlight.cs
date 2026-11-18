using System.Collections.Generic;
using DeadCargo.Flight;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.AI
{
    /// <summary>
    /// How an AI pilot handles its ship. Behaviours say where to point and how fast to go; this turns that into the
    /// kit engines' inputs every frame, in the ship's own axes and full 3D:
    /// <list type="bullet">
    /// <item>Turns go as fast as the ship's steering allows and stop on the heading without wobble.</item>
    /// <item>Hard turns bank and pull through like a fighter; straight flight rolls back to level.</item>
    /// <item>Speed is held, not throttled: the ship slows to turn tight and boosts only when asked and on course.</item>
    /// <item>The side and vertical thrusters cancel sideslip and push into the turn.</item>
    /// <item>The kit's collision avoidance bends the heading and sheds speed as the risk grows, instead of taking over.</item>
    /// </list>
    /// Unlike the kit's steering it never measures against the world's horizon, so nothing caps a climb or a dive.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PilotFlight : MonoBehaviour
    {
        [Header("Avoidance")]
        [Tooltip("The kit's collision avoidance. It scans every frame the pilot flies; this blends its way out into the heading.")]
        [SerializeField] private ObstacleAvoidanceBehaviour avoidance;

        [Tooltip("Collision risk at which avoidance starts to bend the heading, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float avoidanceStart = 0.1f;

        [Tooltip("Collision risk at which avoidance has the heading entirely, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float avoidanceFull = 0.5f;

        [Tooltip("Share of speed shed when avoidance has the heading entirely, 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float avoidanceSlowdown = 0.6f;

        [Header("Turning")]
        [Tooltip("How quickly small turns close, per second. Higher is snappier; the ship's steering still limits big turns.")]
        [SerializeField, Range(1f, 20f)] private float turnResponse = 5f;

        [Tooltip("Share of the ship's steering the pilot counts on to stop a turn, 0 to 1. Lower stops early and never overshoots; higher turns harder.")]
        [SerializeField, Range(0.1f, 1f)] private float brakingShare = 0.7f;

        [Tooltip("How hard the pilot holds the turn rate it wants, per second.")]
        [SerializeField, Range(1f, 40f)] private float rateGain = 12f;

        [Header("Banking")]
        [Tooltip("Turns larger than this start to bank into the turn, in degrees.")]
        [SerializeField, Range(0f, 90f)] private float bankStart = 20f;

        [Tooltip("Turns this large bank fully, pulling through like a fighter, in degrees.")]
        [SerializeField, Range(0f, 180f)] private float bankFull = 70f;

        [Tooltip("How strongly straight flight rolls back to level, 0 to 1. Level is the planet's horizon near a planet, the sector's up elsewhere.")]
        [SerializeField, Range(0f, 1f)] private float levelling = 0.5f;

        [Header("Speed")]
        [Tooltip("Share of the wanted speed kept while the heading is far off the nose, 0 to 1. Lower turns tighter.")]
        [SerializeField, Range(0f, 1f)] private float cornerSpeed = 0.45f;

        [Tooltip("How quickly the speed is corrected, per second.")]
        [SerializeField, Range(0.5f, 10f)] private float speedGain = 2f;

        [Tooltip("How much the side and vertical thrusters help, 0 to 1: they cancel sideslip and push towards the heading.")]
        [SerializeField, Range(0f, 1f)] private float thrusterAssist = 0.6f;

        [Tooltip("Boost only while the nose is within this angle of the heading, in degrees.")]
        [SerializeField, Range(1f, 90f)] private float boostAngle = 15f;

        private Transform ship;
        private Rigidbody body;
        private VehicleEngines3D engines;
        private Vehicle playerShip;
        private AtmosphereFlight playerAir;

        /// <summary>How much avoidance bent the last heading, 0 to 1.</summary>
        public float AvoidanceWeight { get; private set; }

        /// <summary>The heading flown last, after avoidance. World space, unit length.</summary>
        public Vector3 Heading { get; private set; } = Vector3.forward;

        public bool IsBoosting { get; private set; }

        /// <summary>The ship's top speed without boost, in metres a second.</summary>
        public float TopSpeed { get; private set; }

        /// <summary>Share of the ship's steering the pilot uses, 0.1 to 1. Set from the pilot's skill.</summary>
        public float SteeringShare { get; private set; } = 1f;

        /// <summary>Sets the share of the ship's steering the pilot uses, 0.1 to 1.</summary>
        public void SetSteeringShare(float share)
        {
            SteeringShare = Mathf.Clamp(share, 0.1f, 1f);
        }

        /// <summary>Takes the controls of a vehicle. Returns false when it has no 3D engines or rigidbody.</summary>
        public bool Bind(Vehicle vehicle)
        {
            ship = null;
            body = null;
            engines = vehicle != null ? vehicle.GetComponent<VehicleEngines3D>() : null;
            if (engines == null || vehicle.CachedRigidbody == null)
                return false;

            ship = vehicle.transform;
            body = vehicle.CachedRigidbody;
            TopSpeed = engines.GetCurrentMaxSpeedByAxis(false).z;
            if (float.IsInfinity(TopSpeed) || float.IsNaN(TopSpeed))
                TopSpeed = 100f;

            if (avoidance != null)
                avoidance.SetVehicle(vehicle);
            return avoidance == null || avoidance.Initialized;
        }

        /// <summary>Lets go of the controls: no steering, thrust or boost.</summary>
        public void Release()
        {
            if (engines != null)
                engines.ClearAllInputs();
            IsBoosting = false;
        }

        /// <summary>Flies at a point. See <see cref="Fly"/>.</summary>
        public void FlyTo(Vector3 point, float speedShare, bool boost)
        {
            if (ship != null)
                Fly(point - ship.position, speedShare, boost);
        }

        /// <summary>
        /// Flies the heading (world space) at a share of top speed (0 to 1). With boost the ship also boosts while its
        /// nose is on the heading. Call once a frame while the pilot has the controls.
        /// </summary>
        public void Fly(Vector3 heading, float speedShare, bool boost)
        {
            if (ship == null)
                return;

            Vector3 direction = heading.sqrMagnitude > 1e-6f ? heading.normalized : ship.forward;
            float speed = Mathf.Clamp01(speedShare) * TopSpeed;

            // The kit's avoidance steers on its own when it scans; everything below sets the inputs again after it.
            AvoidanceWeight = 0f;
            if (avoidance != null && avoidance.Initialized)
            {
                avoidance.BehaviourUpdate();
                AvoidanceWeight = PilotMath.Smoothstep(avoidanceStart, avoidanceFull, avoidance.ObstacleAvoidanceStrength);
                if (AvoidanceWeight > 0f)
                {
                    direction = PilotMath.Blend(direction, AvoidanceDirection(), AvoidanceWeight);
                    speed *= 1f - AvoidanceWeight * avoidanceSlowdown;
                }
            }

            Heading = direction;
            Vector3 localHeading = ship.InverseTransformDirection(direction);

            Vector3 error = PilotMath.AttitudeError(localHeading, ship.InverseTransformDirection(LevelUp()), bankStart, bankFull, levelling);
            // Turns are planned on the share of steering the pilot dares to use, so a cautious pilot still stops on its mark.
            Vector3 rate = ship.InverseTransformDirection(body.angularVelocity);
            Vector3 power = engines.MaxSteeringForces * SteeringShare;
            float damping = body.angularDamping;
            engines.SetSteeringInputs(SteeringShare * new Vector3(
                PilotMath.SteeringInput(error.x, rate.x, power.x, damping, turnResponse, brakingShare, rateGain),
                PilotMath.SteeringInput(error.y, rate.y, power.y, damping, turnResponse, brakingShare, rateGain),
                PilotMath.SteeringInput(error.z, rate.z, power.z, damping, turnResponse, brakingShare, rateGain)));

            Vector3 velocity = ship.InverseTransformDirection(body.linearVelocity);
            engines.SetMovementInputs(PilotMath.ThrustInputs(localHeading, velocity, speed, cornerSpeed, thrusterAssist,
                body.mass, body.linearDamping, engines.MaxMovementForces, speedGain));

            IsBoosting = boost && localHeading.z >= Mathf.Cos(boostAngle * Mathf.Deg2Rad);
            engines.SetBoostInputs(IsBoosting ? Vector3.forward : Vector3.zero);
        }

        // The kit's own way out: the nose plus each threat's escape direction, weighted by its share of the risk.
        private Vector3 AvoidanceDirection()
        {
            List<ObstacleData> threats = avoidance.ObstacleDataList;
            float totalRisk = 0f;
            for (int i = 0; i < threats.Count; ++i)
                totalRisk += threats[i].riskFactor;

            Vector3 way = ship.forward;
            if (totalRisk > 1e-4f)
            {
                for (int i = 0; i < threats.Count; ++i)
                    way += threats[i].currentAvoidanceDirection * (threats[i].riskFactor / totalRisk);
            }

            return way;
        }

        // Hunters fight near the player, so they level to the player's horizon: the planet's near one, the sector's up elsewhere.
        private Vector3 LevelUp()
        {
            GameAgentManager manager = GameAgentManager.Instance;
            GameAgent player = manager != null ? manager.FocusedGameAgent : null;
            Vehicle vehicle = player != null ? player.Vehicle : null;
            if (vehicle != playerShip)
            {
                playerShip = vehicle;
                playerAir = vehicle != null ? vehicle.GetComponent<AtmosphereFlight>() : null;
            }

            return playerAir != null ? playerAir.Up : Vector3.up;
        }
    }
}
