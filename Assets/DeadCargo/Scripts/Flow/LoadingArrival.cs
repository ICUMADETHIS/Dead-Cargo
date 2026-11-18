using DeadCargo.Cameras;
using DeadCargo.UI;
using DeadCargo.Visuals;
using UnityEngine;
using UnityEngine.UI;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Flow
{
    /// <summary>
    /// The arrival in a sector (phase 6.6 step 4), played by the loading screen in place of its fade from black. The card
    /// gives way to a white flash that fades as the ship streaks in to its start (<see cref="ArrivalStreak"/>). The camera
    /// holds at its chase view throughout, and a comic burst pops where the ship stops. The player's controls come back once
    /// the ship has stopped and settled (<see cref="ShipSettled"/>); the flash and burst play out a moment longer.
    /// </summary>
    internal sealed class LoadingArrival
    {
        private const float FlashTime = 0.25f;

        // The burst starts this long before the ship stops, so it is popping as the ship lands.
        private const float BurstLead = 0.12f;

        // From the ship stopping to the controls coming back.
        private const float SettleTime = 0.1f;

        // Long enough for the burst to pop, hold and shrink away (HudBurst's life).
        private const float BurstLife = 0.6f;
        private const float BurstSize = 300f;

        private readonly RectTransform canvas;
        private readonly Image flash;
        private readonly HudBurst burst;

        private ArrivalStreak streak;
        private ChaseCamera chase;
        private Vector3 destination;
        private float age;

        internal LoadingArrival(RectTransform canvasRect)
        {
            canvas = canvasRect;
            burst = HudFactory.Anchored("Arrival Burst", canvas, new Vector2(0.5f, 0.5f), BurstSize, BurstSize).gameObject.AddComponent<HudBurst>();
            burst.SetAge(-1f);
            flash = HudFactory.Overlay("Arrival Flash", canvas, new Color(1f, 1f, 1f, 0f));
            flash.enabled = false;
        }

        /// <summary>True from <see cref="TryBegin"/> until the ship has stopped and settled.</summary>
        internal bool IsPlaying { get; private set; }

        /// <summary>True once the ship has stopped at its start and settled, when the controls can come back.</summary>
        internal bool ShipSettled => IsPlaying && ShipStopped && age >= ArrivalStreak.Duration + SettleTime;

        /// <summary>True once the ship has settled and the flash and the burst have played out too.</summary>
        internal bool IsOver => !IsPlaying || (ShipSettled && age >= ArrivalStreak.Duration - BurstLead + BurstLife);

        private bool ShipStopped => streak == null || streak.IsDone;

        /// <summary>
        /// Starts the arrival with the screen white, when the player flies a ship that the chase camera follows from
        /// outside it. Returns false and changes nothing otherwise, such as on foot or from the cockpit.
        /// </summary>
        internal bool TryBegin()
        {
            GameAgentManager manager = GameAgentManager.Instance;
            GameAgent player = manager != null ? manager.FocusedGameAgent : null;
            Vehicle ship = player != null && player.IsPlayer ? player.Vehicle : null;
            Rigidbody body = ship != null ? ship.GetComponent<Rigidbody>() : null;
            ChaseCamera camera = ChaseCamera.Active;
            if (body == null || camera == null || camera.Body != body || !camera.Hold())
                return false;

            chase = camera;
            streak = ArrivalStreak.Play(ship.gameObject);
            destination = streak.Destination;
            age = 0f;
            IsPlaying = true;
            SetFlash(1f);
            return true;
        }

        /// <summary>Moves the flash and the burst on, and gives the camera back once the ship has stopped.</summary>
        internal void Tick(float deltaTime)
        {
            if (!IsPlaying)
                return;

            age += deltaTime;
            SetFlash(1f - age / FlashTime);
            if (ShipStopped)
                ReleaseCamera();

            SetBurst(age - (ArrivalStreak.Duration - BurstLead));
        }

        /// <summary>Ends the arrival at once, the ship at its start and the camera following. For a load cut short.</summary>
        internal void Abort()
        {
            if (!IsPlaying)
                return;

            if (streak != null && !streak.IsDone)
                Object.Destroy(streak);

            ReleaseCamera();
            IsPlaying = false;
        }

        private void ReleaseCamera()
        {
            if (chase != null)
                chase.Release();
            chase = null;
        }

        private void SetFlash(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            flash.color = new Color(1f, 1f, 1f, alpha);
            flash.enabled = alpha > 0f;
        }

        // Over where the ship stops, at its age in seconds. Hidden while that point is behind the camera.
        private void SetBurst(float burstAge)
        {
            Camera view = Camera.main;
            Vector3 screen = view != null ? view.WorldToScreenPoint(destination) : Vector3.back;
            if (burstAge < 0f || screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out Vector2 local))
            {
                burst.SetAge(-1f);
                return;
            }

            burst.rectTransform.anchoredPosition = local;
            burst.SetAge(burstAge);
        }
    }
}
