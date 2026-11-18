using UnityEngine;

namespace DeadCargo.Visuals
{
    /// <summary>
    /// The ship's jump out of a run (phase 6.17): it stretches along its nose and streaks away at thousands of metres a
    /// second, trailing cel-banded light with an ink edge, while the camera holds where it was and watches it go. Added to the
    /// ship as the run is extracted, by either route. It owns the ship for <see cref="Duration"/>, after which the report's
    /// flash covers the screen. Collisions are off, so nothing in the way ends the jump early.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JumpStreak : MonoBehaviour
    {
        /// <summary>Seconds from the jump until the ship is gone, when the report's flash begins.</summary>
        public const float Duration = 0.7f;

        private const float StretchTime = 0.2f;
        private const float Stretch = 6f;
        private const float Thinning = 0.6f;
        private const float LaunchDelay = 0.08f;
        private const float LaunchTime = 0.45f;
        private const float LaunchSpeed = 4000f;

        private Rigidbody body;
        private Vector3 baseScale;
        private Vector3 heading;
        private float startSpeed;
        private float elapsed;

        /// <summary>Where the ship left from, in the world.</summary>
        public Vector3 Origin { get; private set; }

        /// <summary>Starts the jump on a ship, or returns the one already playing.</summary>
        public static JumpStreak Play(GameObject ship)
        {
            return ship.TryGetComponent(out JumpStreak playing) ? playing : ship.AddComponent<JumpStreak>();
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            baseScale = transform.localScale;
            heading = transform.forward;
            Origin = transform.position;

            if (body != null)
            {
                startSpeed = Mathf.Max(0f, Vector3.Dot(body.linearVelocity, heading));
                body.detectCollisions = false;
                body.constraints = RigidbodyConstraints.FreezeRotation;
                body.angularVelocity = Vector3.zero;
            }

            StreakTrails.Add(transform, "Jump Trails");
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float stretch = EaseOut(Mathf.Clamp01(elapsed / StretchTime));
            float across = Mathf.Lerp(1f, Thinning, stretch);
            transform.localScale = new Vector3(baseScale.x * across, baseScale.y * across, baseScale.z * Mathf.Lerp(1f, Stretch, stretch));
        }

        private void FixedUpdate()
        {
            float speed = Speed();
            if (body == null)
            {
                transform.position += heading * (speed * Time.fixedDeltaTime);
                return;
            }

            body.linearVelocity = heading * speed;
            body.angularVelocity = Vector3.zero;
        }

        // Slow to leave, then gone: the speed builds on a square.
        private float Speed()
        {
            float launch = Mathf.Clamp01((elapsed - LaunchDelay) / LaunchTime);
            return Mathf.Lerp(startSpeed, LaunchSpeed, launch * launch);
        }

        private static float EaseOut(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }
    }
}
