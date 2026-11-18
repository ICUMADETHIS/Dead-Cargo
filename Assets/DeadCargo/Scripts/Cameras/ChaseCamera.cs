using UnityEngine;
using VSX.CameraSystem;
using VSX.UniversalVehicleCombat;
using VSX.UniversalVehicleCombat.Space;

namespace DeadCargo.Cameras
{
    /// <summary>
    /// The camera behind the player's ship, in place of the kit's space fighter camera (phase 6.16). The kit's camera moves
    /// on the 50 Hz physics step, so above 50 frames a second the view stepped. This one moves every rendered frame from the
    /// ship's interpolated pose, early in the frame so the HUD's markers project from where the camera is. The framing is the
    /// kit's: it eases towards the selected view target, drifts sideways with roll and yaw, looks along the ship's nose with
    /// the roll lagging, and widens while boosting. The kit view targets' strengths, which are fractions per physics step,
    /// become rates per second, so the look is the same at any frame rate.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class ChaseCamera : VehicleCameraController
    {
        // The kit's follow strengths are fractions per physics step, at 50 steps a second.
        private const float KitStepsPerSecond = 50f;

        // A move further than this between frames is a teleport, such as a debug jump: the camera cuts instead of chasing.
        private const float CutDistance = 200f;

        // Watching the ship jump out: how fast the camera turns after it, and where it steps back to from the cockpit, in metres.
        private const float WatchTurnRate = 12f;
        private const float WatchMinDistance = 15f;
        private const float WatchStandOff = 30f;
        private const float WatchRise = 8f;

        [Header("Dead Cargo")]
        [Tooltip("The kit's space fighter camera this replaces. Switched off while this camera is smooth, on while it is not.")]
        [SerializeField] private SpaceFighterCameraController kitCamera;

        [Tooltip("Field of view while boosting, in degrees. Set from the kit camera's by Set Up Sector.")]
        [SerializeField, Range(30f, 120f)] private float boostFieldOfView = 80f;

        [Tooltip("How quickly the field of view eases towards and away from the boost view, per second.")]
        [SerializeField, Min(0.1f)] private float fieldOfViewRate = 6f;

        private Rigidbody body;
        private Engines engines;
        private bool smooth = true;
        private bool held;
        private Transform watched;

        /// <summary>The chase camera in the loaded scenes, or null.</summary>
        public static ChaseCamera Active { get; private set; }

        /// <summary>True while this camera follows every frame; false while the kit's camera follows on the physics step.</summary>
        public bool IsSmooth => smooth;

        /// <summary>The followed ship's rigidbody, or null while the camera follows no ship.</summary>
        public Rigidbody Body => body;

        /// <summary>True while the camera is held still by <see cref="Hold"/>.</summary>
        public bool IsHeld => held;

        // Statics survive play sessions when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
        }

        private void OnEnable()
        {
            Active = this;
            if (kitCamera != null)
                kitCamera.enabled = !smooth;
        }

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        /// <summary>
        /// Switches between this camera and the kit's, for comparing them. The kit's camera goes with the ship's old
        /// physics, uninterpolated, so it looks exactly as before.
        /// </summary>
        public void SetSmooth(bool on)
        {
            smooth = on;
            if (kitCamera != null)
                kitCamera.enabled = !on;

            ApplyInterpolation();
        }

        /// <summary>
        /// Stops following and holds the camera where it is, turning it to keep <paramref name="target"/> in view, for the jump
        /// out (phase 6.17). From the cockpit it first steps back behind the ship, so it is not carried off inside it.
        /// </summary>
        public void Watch(Transform target)
        {
            watched = target;
            if (target == null || cameraEntity == null)
                return;

            if (kitCamera != null)
                kitCamera.enabled = false;

            Transform rig = cameraEntity.transform;
            if (rig.parent != null)
                rig.SetParent(null, true);

            if ((rig.position - target.position).sqrMagnitude < WatchMinDistance * WatchMinDistance)
            {
                rig.position = target.position - target.forward * WatchStandOff + target.up * WatchRise;
                rig.rotation = Quaternion.LookRotation(target.position - rig.position, target.up);
            }
        }

        /// <summary>
        /// Stops following and holds the camera exactly where it is, for the ship's arrival (phase 6.6): the ship streaks in
        /// to its start while the camera waits at its chase view. Returns false, holding nothing, when the camera rides
        /// inside the ship, as from the cockpit, since it would be carried along.
        /// </summary>
        public bool Hold()
        {
            if (cameraEntity == null || cameraEntity.transform.parent != null)
                return false;

            held = true;
            if (kitCamera != null)
                kitCamera.enabled = false;
            return true;
        }

        /// <summary>Follows the ship again after <see cref="Hold"/>.</summary>
        public void Release()
        {
            if (!held)
                return;

            held = false;
            if (kitCamera != null)
                kitCamera.enabled = !smooth;
        }

        protected override bool Initialize(CameraTarget newTarget)
        {
            if (!base.Initialize(newTarget))
                return false;

            body = newTarget.GetComponent<Rigidbody>();
            engines = newTarget.GetComponent<Engines>();
            ApplyInterpolation();
            return true;
        }

        // Moving the camera on the physics step is what made the view step.
        protected override void CameraControllerFixedUpdate()
        {
        }

        protected override void CameraControllerUpdate()
        {
            if (held)
                return;

            float deltaTime = Time.deltaTime;
            if (watched != null)
            {
                WatchUpdate(deltaTime);
                return;
            }

            CameraViewTarget view = cameraEntity.CurrentViewTarget;
            if (!smooth || view == null || deltaTime <= 0f)
                return;

            // A view that parents the camera carries it along by the hierarchy.
            Transform rig = cameraEntity.transform;
            if (!view.ParentCameraOnSelected || rig.parent != view.transform)
            {
                rig.position = FollowPosition(view, rig.position, deltaTime);
                rig.rotation = FollowRotation(view, rig.rotation, deltaTime);
            }

            UpdateFieldOfView(deltaTime);
        }

        // The view locks would pull the camera back onto the ship while it is watched leaving or held for its arrival.
        protected override void CameraControllerLateUpdate()
        {
            if (watched == null && !held)
                base.CameraControllerLateUpdate();
        }

        private void WatchUpdate(float deltaTime)
        {
            Transform rig = cameraEntity.transform;
            Vector3 toTarget = watched.position - rig.position;
            if (deltaTime <= 0f || toTarget.sqrMagnitude < 0.01f)
                return;

            Quaternion goal = Quaternion.LookRotation(toTarget, rig.up);
            rig.rotation = Quaternion.Slerp(rig.rotation, goal, 1f - Mathf.Exp(-WatchTurnRate * deltaTime));
        }

        private Vector3 FollowPosition(CameraViewTarget view, Vector3 current, float deltaTime)
        {
            Transform target = view.transform;
            if (view.LockPosition)
                return target.position;

            // Sideways with roll and yaw, as the kit's fighter camera drifts.
            float lateral = 0f;
            if (body != null)
            {
                Vector3 spin = target.InverseTransformDirection(body.angularVelocity);
                lateral = view.SpinOffsetCoefficient * -spin.z + view.YawLateralOffset * spin.y;
            }

            Vector3 goal = target.TransformPoint(new Vector3(lateral, 0f, 0f));
            if ((goal - current).sqrMagnitude > CutDistance * CutDistance)
                return goal;

            return Vector3.Lerp(current, goal, Ease(view.PositionFollowStrength, deltaTime));
        }

        private static Quaternion FollowRotation(CameraViewTarget view, Quaternion current, float deltaTime)
        {
            Transform target = view.transform;
            Quaternion rotation = view.LockRotation
                ? target.rotation
                : Quaternion.Slerp(current, target.rotation, Ease(view.RotationFollowStrength, deltaTime));

            // Along the nose at once; only the roll trails.
            if (view.LockCameraForwardVector)
                rotation = Quaternion.LookRotation(target.forward, rotation * Vector3.up);
            if (view.LockCameraUpright)
                rotation = Quaternion.LookRotation(rotation * Vector3.forward, Vector3.up);

            return rotation;
        }

        private void UpdateFieldOfView(float deltaTime)
        {
            if (engines == null || !engines.EnginesActivated || cameraEntity.MainCamera == null)
                return;

            float goal = Mathf.Lerp(cameraEntity.DefaultFieldOfView, boostFieldOfView, Mathf.Clamp01(engines.BoostInputs.z));
            float current = cameraEntity.MainCamera.fieldOfView;
            cameraEntity.SetFieldOfView(Mathf.Lerp(current, goal, 1f - Mathf.Exp(-fieldOfViewRate * deltaTime)));
        }

        // The ship is drawn between physics steps only while this camera follows it every frame.
        private void ApplyInterpolation()
        {
            if (body != null)
                body.interpolation = smooth ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
        }

        // A kit strength, the share closed per physics step, applied over a frame of any length.
        private static float Ease(float strengthPerStep, float deltaTime)
        {
            return 1f - Mathf.Pow(1f - Mathf.Clamp01(strengthPerStep), deltaTime * KitStepsPerSecond);
        }
    }
}
