using DeadCargo.Flight;
using UnityEngine;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// The ship's arrival in a sector (phase 6.6 step 4), the jump-out (<see cref="JumpStreak"/>) in reverse. The ship
    /// streaks in nose first from far behind its start, stretched along its nose and trailing the streak's light. It slows
    /// hard and shrinks back to its own size as it reaches its start, where it stops dead. The loading screen plays it as
    /// the scene appears, while the camera holds at its chase view. It owns the ship for <see cref="Duration"/>. Collisions
    /// are off until it stops, so nothing on the way in ends it early. Removing it early puts the ship at its start.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArrivalStreak : MonoBehaviour
    {
        /// <summary>Seconds from the start of the arrival until the ship stops at its start.</summary>
        public const float Duration = 0.6f;

        // How far behind its start the ship comes in from, in metres. It closes on a cube, so it starts at 4,500 m/s.
        private const float Distance = 900f;
        private const float Stretch = 6f;
        private const float Thinning = 0.6f;
        private const string TrailName = "Arrival Trails";

        private Rigidbody body;
        private Vector3 baseScale;
        private Vector3 start;
        private Vector3 heading;
        private RigidbodyConstraints constraints;
        private bool detectedCollisions;
        private GameObject trails;
        private float flightTime;
        private float shapeTime;

        /// <summary>Where the ship stops: its start, in the world.</summary>
        public Vector3 Destination => start;

        /// <summary>True once the ship has stopped at its start and is its own again.</summary>
        public bool IsDone { get; private set; }

        /// <summary>Starts the arrival on a ship standing at its start, or returns the one already playing.</summary>
        public static ArrivalStreak Play(GameObject ship)
        {
            return ship.TryGetComponent(out ArrivalStreak playing) ? playing : ship.AddComponent<ArrivalStreak>();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            baseScale = transform.localScale;
            start = body != null ? body.position : transform.position;
            heading = transform.forward;

            if (body != null)
            {
                constraints = body.constraints;
                detectedCollisions = body.detectCollisions;
                body.detectCollisions = false;
                body.constraints = RigidbodyConstraints.FreezeRotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            // A clean jump back, so no in-between frame is drawn; the trails start from there.
            RigShift.Move(transform, PositionAt(0f) - start);
            ApplyShape(0f);
            trails = StreakTrails.Add(transform, TrailName);
        }

        private void FixedUpdate()
        {
            if (IsDone)
                return;

            flightTime += Time.fixedDeltaTime;
            float share = Mathf.Clamp01(flightTime / Duration);
            if (share >= 1f)
            {
                Finish();
                return;
            }

            // The velocity that lands the ship on its path at the end of this step, so it never drifts off it.
            Vector3 goal = PositionAt(share);
            if (body == null)
            {
                transform.position = goal;
                return;
            }

            body.linearVelocity = (goal - body.position) / Time.fixedDeltaTime;
            body.angularVelocity = Vector3.zero;
        }

        private void Update()
        {
            if (IsDone)
                return;

            shapeTime += Time.deltaTime;
            ApplyShape(Mathf.Clamp01(shapeTime / Duration));
        }

        private void OnDestroy()
        {
            if (!IsDone)
                Restore();
        }

        private Vector3 PositionAt(float share)
        {
            return start - heading * (Distance * (1f - Ease(share)));
        }

        // Long and thin while fast, its own size as it stops.
        private void ApplyShape(float share)
        {
            float settled = Ease(share);
            float across = Mathf.Lerp(Thinning, 1f, settled);
            transform.localScale = new Vector3(baseScale.x * across, baseScale.y * across, baseScale.z * Mathf.Lerp(Stretch, 1f, settled));
        }

        private void Finish()
        {
            Restore();
            Destroy(this);
        }

        // Puts the ship at its start, still, its own size and solid again.
        private void Restore()
        {
            IsDone = true;
            transform.localScale = baseScale;
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = start;
                body.constraints = constraints;
                body.detectCollisions = detectedCollisions;
            }

            transform.position = start;
            StreakTrails.Stop(trails);
            trails = null;
        }

        // Fast, then a hard stop: what is left of the way shrinks on a cube.
        private static float Ease(float share)
        {
            float left = 1f - share;
            return 1f - left * left * left;
        }
    }
}
