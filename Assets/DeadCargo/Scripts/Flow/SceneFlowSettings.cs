using System.Collections.Generic;
using UnityEngine;

namespace DeadCargo.Flow
{
    /// <summary>
    /// The scenes the game moves between, how long the loading screen stays up, and the tips it shows. Lives in a Resources
    /// folder so the scene flow can find it from any scene. Paths are project paths, such as
    /// Assets/DeadCargo/Scenes/Mothership.unity.
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "Dead Cargo/Scene Flow Settings")]
    public sealed class SceneFlowSettings : ScriptableObject
    {
        public const string ResourceName = "SceneFlowSettings";

        [Tooltip("The hub scene the game starts in and returns to after a run.")]
        [SerializeField] private string mothershipScene;

        [Tooltip("The sector scene a run launches into.")]
        [SerializeField] private string sectorScene;

        [Tooltip("Least time the loading card stays up for any load, in seconds, counted from when the screen is dark.")]
        [SerializeField, Range(0f, 10f)] private float minimumLoadSeconds = 0.8f;

        [Tooltip("Least time the loading card stays up for a load into the sector scene, launches and restarts alike, in " +
                 "seconds, so its tip can be read. The bar fills over this time rather than waiting full.")]
        [SerializeField, Range(0f, 10f)] private float sectorLoadSeconds = 3f;

        [Tooltip("Tips the loading screen shows, a different one each load. Keys named here are the defaults; update them " +
                 "if the bindings change.")]
        [SerializeField, TextArea(1, 3)] private string[] loadingTips =
        {
            "Press H to spool the jump drive. It needs dark matter aboard, and only builds while you fly slowly.",
            "Hits knock the jump spool back. Find a quiet spot before you spool.",
            "A heavy hold spools slower, flies slower and turns wider.",
            "Starting an extraction point's uplink (U) broadcasts where you are. Expect company.",
            "Once an extraction point opens, fly into it before its window closes.",
            "Extraction points burn no dark matter. The jump drive does.",
            "Firing and taking cargo aboard raise heat. It cools after a few quiet seconds.",
            "Lose the run and you lose the hold. Lose the ship and you pay for a new one.",
            "Press I to open the hold. Drag cargo to repack it, and R turns a piece.",
            "J jettisons cargo. Hold it to dump the lot.",
            "Press B to cruise to a planet. An asteroid field on the route blocks the drive.",
            "Every run draws a new sector: new rocks, new sky, new planets."
        };

        public string MothershipScene => mothershipScene;
        public string SectorScene => sectorScene;

        /// <summary>Least time the loading card stays up for any load, in seconds.</summary>
        public float MinimumLoadSeconds => minimumLoadSeconds;

        /// <summary>Least time the loading card stays up for a load into the sector scene, in seconds.</summary>
        public float SectorLoadSeconds => sectorLoadSeconds;

        /// <summary>The loading screen's tips. Empty entries are skipped.</summary>
        public IReadOnlyList<string> LoadingTips => loadingTips;
    }
}
