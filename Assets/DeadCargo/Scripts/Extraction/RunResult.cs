using System;
using System.Collections.Generic;
using DeadCargo.Cargo;
using DeadCargo.Heat;

namespace DeadCargo.Extraction
{
    /// <summary>The outcome, statistics and cargo of a finished run.</summary>
    public sealed class RunResult
    {
        private readonly CargoStack[] cargo;

        public RunResult(RunOutcome outcome, float duration, HeatTier? peakHeat, int authorityShipsDestroyed, CargoStack[] cargo)
        {
            Outcome = outcome;
            Duration = duration;
            PeakHeat = peakHeat;
            AuthorityShipsDestroyed = authorityShipsDestroyed;
            this.cargo = cargo ?? Array.Empty<CargoStack>();

            float mass = 0f;
            int value = 0;
            foreach (CargoStack stack in this.cargo)
            {
                mass += stack.Mass;
                value += stack.Value;
            }

            CargoMass = mass;
            CargoValue = value;
        }

        public RunOutcome Outcome { get; }

        /// <summary>Run length, in seconds.</summary>
        public float Duration { get; }

        /// <summary>The highest heat tier reached, or null when the scene has no heat tracker.</summary>
        public HeatTier? PeakHeat { get; }

        public int AuthorityShipsDestroyed { get; }

        /// <summary>The hold's contents when the run ended: banked on extraction, gone on a loss.</summary>
        public IReadOnlyList<CargoStack> Cargo => cargo;

        /// <summary>Total cargo mass, in tonnes.</summary>
        public float CargoMass { get; }

        /// <summary>Total cargo value, in credits.</summary>
        public int CargoValue { get; }
    }
}
