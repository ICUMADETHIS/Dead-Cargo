#if UNITY_EDITOR || DEVELOPMENT_BUILD || DDT_ENABLED
using DeadCargo.Cargo;
using UnityEngine;
using UnityEngine.SceneManagement;
using VSX.UniversalVehicleCombat;
using VSX.UniversalVehicleCombat.Radar;

namespace DeadCargo.Debugging
{
    /// <summary>
    /// What every readout and world view shares: the frame stats, the camera, and the player's ship with the parts the
    /// readouts use, looked up once when the ship changes rather than on every refresh.
    /// </summary>
    internal sealed class DebugContext
    {
        private Vehicle cachedVehicle;
        private Vector3 lastCameraPosition;
        private bool hasCameraPosition;

        public DebugContext(DebugFrameStats stats)
        {
            Stats = stats;
        }

        public DebugFrameStats Stats { get; }
        public Camera Camera { get; private set; }

        /// <summary>The ship the player is flying, or null on foot or between scenes.</summary>
        public Vehicle Player { get; private set; }

        public Health PlayerHealth { get; private set; }
        public TargetSelector PlayerTargets { get; private set; }
        public CargoHold PlayerHold { get; private set; }

        /// <summary>The camera's velocity, in metres per second, smoothed over a few frames.</summary>
        public Vector3 CameraVelocity { get; private set; }

        /// <summary>Main-thread time the overlay itself took last frame, in milliseconds.</summary>
        public float OverlayMs { get; set; }

        /// <summary>Rigidbodies the Physics channel found around the camera last frame.</summary>
        public int NearbyBodies { get; set; }

        public string SceneName { get; private set; } = string.Empty;

        /// <summary>Where distances are measured from: the player's ship when flying, else the camera.</summary>
        public Vector3 Focus => Player != null ? Player.transform.position : Camera != null ? Camera.transform.position : Vector3.zero;

        /// <summary>Looks up the camera and the player's ship. Call every frame before drawing.</summary>
        public void Update(float unscaledDeltaTime)
        {
            Camera = Camera.main;
            if (Camera != null)
            {
                Vector3 position = Camera.transform.position;
                if (hasCameraPosition && unscaledDeltaTime > 0f)
                    CameraVelocity = Vector3.Lerp(CameraVelocity, (position - lastCameraPosition) / unscaledDeltaTime, 0.2f);

                lastCameraPosition = position;
                hasCameraPosition = true;
            }
            else
            {
                hasCameraPosition = false;
                CameraVelocity = Vector3.zero;
            }

            Vehicle vehicle = FindPlayerVehicle();
            if (vehicle == cachedVehicle)
                return;

            cachedVehicle = vehicle;
            Player = vehicle;
            PlayerHealth = vehicle != null ? vehicle.GetComponentInChildren<Health>() : null;
            PlayerTargets = vehicle != null ? vehicle.GetComponentInChildren<TargetSelector>() : null;
            PlayerHold = vehicle != null ? vehicle.GetComponent<CargoHold>() : null;
        }

        public void OnSceneChanged(Scene scene)
        {
            SceneName = scene.name;
            hasCameraPosition = false;
        }

        private static Vehicle FindPlayerVehicle()
        {
            GameAgentManager manager = GameAgentManager.Instance;
            GameAgent agent = manager != null ? manager.FocusedGameAgent : null;
            if (agent == null || !agent.IsPlayer)
                return null;

            Vehicle vehicle = agent.Vehicle;
            return vehicle != null && !vehicle.Destroyed ? vehicle : null;
        }
    }
}
#endif
