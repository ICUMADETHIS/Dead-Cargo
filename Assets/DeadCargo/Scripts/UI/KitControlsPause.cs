using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.UI
{
    /// <summary>
    /// Lends the mouse to a HUD screen in flight. It frees the cursor, holds the kit's steering so the ship keeps its heading
    /// (throttle, strafe and boost still work), and stops the kit's weapon triggers so clicks do not fire. Ending puts back
    /// only what it changed, and recentres the kit's steering reticle, which follows the mouse even while steering is held,
    /// so the ship does not swing to wherever the mouse wandered.
    /// </summary>
    internal sealed class KitControlsPause
    {
        private static readonly FieldInfo ReticleField = typeof(PlayerSpaceVehicleFlightControls).GetField("reticuleViewportPosition",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Vector3 ReticleCentre = new Vector3(0.5f, 0.5f, 0f);

        private readonly List<PlayerSpaceVehicleFlightControls> heldSteering = new List<PlayerSpaceVehicleFlightControls>();
        private readonly List<PlayerTriggerablesInput> heldTriggers = new List<PlayerTriggerablesInput>();
        private CursorLockMode cursorLock;
        private bool cursorVisible;

        internal bool IsActive { get; private set; }

        /// <summary>Takes the mouse from the player's flight controls and weapons.</summary>
        internal void Begin(GameAgent player)
        {
            if (IsActive)
                return;

            IsActive = true;
            cursorLock = Cursor.lockState;
            cursorVisible = Cursor.visible;
            FreeCursor();
            if (player == null)
                return;

            foreach (PlayerSpaceVehicleFlightControls flight in player.GetComponentsInChildren<PlayerSpaceVehicleFlightControls>())
            {
                // Clearing the steering needs the controls' engines, which only an initialised script has.
                if (!flight.Initialized || !flight.InputEnabled)
                    continue;

                flight.DisableSteering(true);
                heldSteering.Add(flight);
            }

            player.GetComponentsInChildren(heldTriggers);
            for (int i = heldTriggers.Count - 1; i >= 0; --i)
            {
                if (heldTriggers[i].InputEnabled)
                    heldTriggers[i].DisableInput();
                else
                    heldTriggers.RemoveAt(i);
            }
        }

        /// <summary>
        /// Holds the mouse for the screen while it is up. The kit's game states lock the cursor again when play resumes,
        /// and can switch input back on, so both are checked every frame of play.
        /// </summary>
        internal void Tick()
        {
            if (!IsActive || Time.timeScale <= 0f)
                return;

            FreeCursor();
            for (int i = 0; i < heldTriggers.Count; ++i)
            {
                if (heldTriggers[i] != null && heldTriggers[i].InputEnabled)
                    heldTriggers[i].DisableInput();
            }
        }

        /// <summary>
        /// Gives the mouse back. With <paramref name="restore"/> false, when the run has ended and the run's own screens own
        /// the controls and cursor now, nothing is put back.
        /// </summary>
        internal void End(bool restore)
        {
            if (!IsActive)
                return;

            IsActive = false;
            if (restore)
            {
                foreach (PlayerSpaceVehicleFlightControls flight in heldSteering)
                {
                    if (flight == null)
                        continue;

                    ReticleField?.SetValue(flight, ReticleCentre);
                    flight.EnableSteering();
                }

                foreach (PlayerTriggerablesInput triggers in heldTriggers)
                {
                    if (triggers != null)
                        triggers.EnableInput();
                }

                Cursor.lockState = cursorLock;
                Cursor.visible = cursorVisible;
            }

            heldSteering.Clear();
            heldTriggers.Clear();
        }

        private static void FreeCursor()
        {
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;
        }
    }
}
