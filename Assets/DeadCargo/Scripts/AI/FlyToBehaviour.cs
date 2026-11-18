using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.AI
{
    /// <summary>
    /// Flies at a goal the pilot sets each frame, through <see cref="PilotFlight"/>. The <see cref="PilotBrain"/>
    /// flies its pursuit, holding and withdrawal with it.
    /// </summary>
    public sealed class FlyToBehaviour : AIVehicleBehaviour
    {
        [Tooltip("Flies the ship.")]
        [SerializeField] private PilotFlight pilotFlight;

        private Vector3 goal;
        private float speedShare = 1f;
        private bool boost;

        /// <summary>Where the ship is flying. World space.</summary>
        public Vector3 Goal => goal;

        /// <summary>Sets where to fly, the speed as a share of top speed (0 to 1), and whether to boost when on course.</summary>
        public void SetGoal(Vector3 point, float speed, bool useBoost)
        {
            goal = point;
            speedShare = Mathf.Clamp01(speed);
            boost = useBoost;
        }

        protected override bool Initialize(Vehicle vehicle)
        {
            return base.Initialize(vehicle) && pilotFlight != null;
        }

        public override bool BehaviourUpdate()
        {
            if (!base.BehaviourUpdate())
                return false;

            pilotFlight.FlyTo(goal, speedShare, boost);
            return true;
        }
    }
}
