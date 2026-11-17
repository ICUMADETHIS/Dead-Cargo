using DeadCargo.Flight;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Controls
{
    /// <summary>
    /// Levels the ship's wings when the player stops rolling: to the horizon of the planet below, or in open space to the
    /// sector's level, as <see cref="AtmosphereFlight.Up"/> gives it. Replaces the kit's auto roll, which only ever levels
    /// to the world's up, so near a planet it kept rolling the ship towards a horizon tilted against the ground. Runs after
    /// the kit's flight controls each frame and changes only the roll they set. Lives under the player's game agent, like
    /// the kit's input scripts.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class LevelFlightInput : VehicleInput
    {
        // Roll input smaller than this is no roll from the player.
        private const float RollDeadZone = 0.0001f;

        [Header("Level Flight")]
        [Tooltip("Seconds after the player last rolled before the wings level.")]
        [SerializeField, Min(0f)] private float delay = 0.5f;

        [Tooltip("Roll input for each degree off level.")]
        [SerializeField, Range(0f, 1f)] private float strength = 0.04f;

        [Tooltip("Strongest roll input the levelling uses, 0 to 1. Set from the kit's auto roll by Set Up Sector.")]
        [SerializeField, Range(0f, 1f)] private float maxRoll = 0.2f;

        private Engines engines;
        private AtmosphereFlight air;
        private float lastRoll;

        /// <summary>Degrees the wings are off level now, signed by the way they lean. 0 pointing straight up or down.</summary>
        public float OffLevel { get; private set; }

        protected override bool Initialize(Vehicle vehicle)
        {
            if (!base.Initialize(vehicle))
                return false;

            engines = vehicle.GetComponentInChildren<Engines>();
            air = vehicle.GetComponent<AtmosphereFlight>();
            lastRoll = Time.time;
            return engines != null;
        }

        protected override void InputUpdate()
        {
            if (engines.ControlsDisabled)
                return;

            // The kit's controls have just set this frame's steering: any roll in it is the player's own.
            Vector3 steering = engines.SteeringInputs;
            if (Mathf.Abs(steering.z) > RollDeadZone)
            {
                lastRoll = Time.time;
                return;
            }

            Transform ship = engines.transform;
            Vector3 up = air != null ? air.Up : Vector3.up;
            OffLevel = RollFromLevel(ship, up, out float steepness);
            if (Time.time - lastRoll < delay)
                return;

            // Pointing straight up or down there is no level to roll to, so the levelling fades out.
            steering.z = Mathf.Clamp(-OffLevel * strength, -1f, 1f) * maxRoll * (1f - steepness);
            engines.SetSteeringInputs(steering);
        }

        // The roll between the ship's wings and the horizon square to up, in degrees, and how steeply the nose points along up.
        private static float RollFromLevel(Transform ship, Vector3 up, out float steepness)
        {
            Vector3 forward = ship.forward;
            steepness = Mathf.Abs(Vector3.Dot(forward, up));
            Vector3 flat = Vector3.ProjectOnPlane(forward, up);
            if (flat.sqrMagnitude < 1e-6f)
                return 0f;

            Vector3 levelRight = Vector3.Cross(up, flat.normalized);
            float angle = Vector3.Angle(levelRight, ship.right);
            return Vector3.Dot(ship.up, levelRight) > 0f ? -angle : angle;
        }
    }
}
