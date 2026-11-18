using System;
using UnityEngine;

namespace DeadCargo.UI
{
    /// <summary>Player options for the HUD, kept between sessions in PlayerPrefs. Read cheaply every frame.</summary>
    public static class HudSettings
    {
        private const string SteeringLineKey = "DeadCargo.Hud.SteeringLine";

        private static bool? steeringLine;

        /// <summary>Raised when any option changes, so the HUD can redraw.</summary>
        public static event Action Changed;

        /// <summary>Whether a line runs out from the reticle to the steering cursor while the player steers. Off by default.</summary>
        public static bool ShowSteeringLine
        {
            get => steeringLine ??= PlayerPrefs.GetInt(SteeringLineKey, 0) == 1;
            set
            {
                if (ShowSteeringLine == value)
                    return;

                steeringLine = value;
                PlayerPrefs.SetInt(SteeringLineKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }
    }
}
