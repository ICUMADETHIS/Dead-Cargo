using UnityEngine;
using UnityEngine.Serialization;
using VSX.UniversalVehicleCombat;
using VSX.UniversalVehicleCombat.Radar;

namespace DeadCargo.AI
{
    /// <summary>
    /// Breaks away from the target in full 3D: picks a heading away from it, off the straight line by up to the break
    /// cone, and extends along it at speed with a slow weave, boosting while the nose is on it. Flown through
    /// <see cref="PilotFlight"/>, so a break can climb or dive as steeply as it turns. Run by a <see cref="DogfightBehaviour"/>.
    /// </summary>
    public sealed class BreakAwayBehaviour : AIVehicleBehaviour
    {
        [Tooltip("Flies the ship.")]
        [SerializeField] private PilotFlight pilotFlight;

        [Header("Break Away")]
        [Tooltip("Largest angle between the break heading and the line straight away from the target, in degrees.")]
        [SerializeField, Range(0f, 150f)] private float breakCone = 75f;

        [Tooltip("Speed while breaking away, as a share of top speed.")]
        [FormerlySerializedAs("throttle")]
        [SerializeField, Range(0f, 1f)] private float speed = 1f;

        [Tooltip("Boost while extending away, once the nose is on the break heading.")]
        [SerializeField] private bool boost = true;

        [Tooltip("How fast the weave wanders. 0 flies the heading straight.")]
        [SerializeField, Min(0f)] private float weaveRate = 0.4f;

        [Tooltip("Largest angle the weave wanders off the heading, in degrees.")]
        [SerializeField, Range(0f, 45f)] private float weaveAngle = 15f;

        private Weapons weapons;
        private Vector3 heading = Vector3.forward;
        private Vector3 weaveFirst = Vector3.right;
        private Vector3 weaveSecond = Vector3.up;
        private float weaveSeed;

        /// <summary>The current break's heading before the weave. World space, unit length.</summary>
        public Vector3 Heading => heading;

        protected override bool Initialize(Vehicle vehicle)
        {
            if (!base.Initialize(vehicle) || pilotFlight == null)
                return false;

            weapons = vehicle.GetComponent<Weapons>();
            return weapons != null;
        }

        public override void StartBehaviour()
        {
            base.StartBehaviour();
            if (state == VehicleBehaviourState.Started)
                PickHeading();
        }

        public override bool BehaviourUpdate()
        {
            if (!base.BehaviourUpdate())
                return false;

            // Perlin noise wanders smoothly on two axes; each break starts from its own seed.
            float time = Time.time * weaveRate;
            float sideways = (Mathf.PerlinNoise(weaveSeed + time, 0f) - 0.5f) * 2f;
            float upways = (Mathf.PerlinNoise(0f, weaveSeed + time) - 0.5f) * 2f;
            pilotFlight.Fly(PilotMath.Deflect(heading, weaveFirst, weaveSecond, sideways, upways, weaveAngle), speed, boost);
            return true;
        }

        private void PickHeading()
        {
            Transform ship = vehicle.transform;
            TargetSelector selector = weapons.WeaponsTargetSelector;
            Trackable target = selector != null ? selector.SelectedTarget : null;
            Vector3 away = target != null ? ship.position - target.transform.position : ship.forward;

            heading = PilotMath.BreakHeading(away, Random.Range(0f, breakCone), Random.value);
            PilotMath.Perpendiculars(heading, out weaveFirst, out weaveSecond);
            weaveSeed = Random.Range(0f, 100f);
        }
    }
}
