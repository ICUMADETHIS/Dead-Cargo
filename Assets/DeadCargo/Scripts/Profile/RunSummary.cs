using System;
using UnityEngine;

namespace DeadCargo.Profile
{
    /// <summary>The saved summary of a finished run, shown on the mothership.</summary>
    [Serializable]
    public struct RunSummary
    {
        [SerializeField] private bool extracted;
        [SerializeField] private float duration;
        [SerializeField] private string peakHeat;
        [SerializeField] private int authorityShipsDestroyed;
        [SerializeField] private int cargoValue;
        [SerializeField] private float cargoMass;

        public RunSummary(bool extracted, float duration, string peakHeat, int authorityShipsDestroyed, int cargoValue, float cargoMass)
        {
            this.extracted = extracted;
            this.duration = duration;
            this.peakHeat = peakHeat;
            this.authorityShipsDestroyed = authorityShipsDestroyed;
            this.cargoValue = cargoValue;
            this.cargoMass = cargoMass;
        }

        /// <summary>True when the ship jumped out; false when it was lost.</summary>
        public bool Extracted => extracted;

        /// <summary>Run length, in seconds.</summary>
        public float Duration => duration;

        /// <summary>Label of the highest heat tier reached, or empty when the sector had no heat.</summary>
        public string PeakHeat => peakHeat;

        public int AuthorityShipsDestroyed => authorityShipsDestroyed;

        /// <summary>Value of the hold at the end of the run, in credits: banked or lost.</summary>
        public int CargoValue => cargoValue;

        /// <summary>Mass of the hold at the end of the run, in tonnes.</summary>
        public float CargoMass => cargoMass;
    }
}
