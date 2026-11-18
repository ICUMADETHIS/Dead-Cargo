using System.Collections.Generic;
using VSX.UniversalVehicleCombat;

namespace DeadCargo.Flow
{
    /// <summary>
    /// Keeps the player's controls off while a new scene is hidden, so nothing moves or fires in the dark. The kit
    /// switches input on as the player's agent starts and boards its ship, so <see cref="Hold"/> runs every frame and
    /// catches each input as it comes on. <see cref="Release"/> switches back on only the inputs it switched off.
    /// </summary>
    internal sealed class PlayerControlsHold
    {
        private readonly List<GeneralInput> held = new List<GeneralInput>();
        private readonly List<GeneralInput> found = new List<GeneralInput>();

        /// <summary>Switches off any of the player's inputs that are on. Call once a frame while holding.</summary>
        internal void Hold()
        {
            GameAgentManager manager = GameAgentManager.Instance;
            GameAgent player = manager != null ? manager.FocusedGameAgent : null;
            if (player == null || !player.IsPlayer)
                return;

            player.GetComponentsInChildren(found);
            foreach (GeneralInput input in found)
            {
                if (input == null || !input.InputEnabled)
                    continue;

                input.DisableInput();
                if (!held.Contains(input))
                    held.Add(input);
            }

            found.Clear();
        }

        /// <summary>Gives back the inputs this hold switched off.</summary>
        internal void Release()
        {
            foreach (GeneralInput input in held)
            {
                if (input != null)
                    input.EnableInput();
            }

            held.Clear();
        }
    }
}
