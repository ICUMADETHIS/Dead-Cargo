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

        public RunResult(RunOutcome outcome, ExtractionRoute route, float duration, HeatTier? peakHeat, int authorityShipsDestroyed,
            CargoStack[] cargo, bool shipLost, int shipLossFee)
        {
            Outcome = outcome;
            Route = route;
            Duration = duration;
            PeakHeat = peakHeat;
            AuthorityShipsDestroyed = authorityShipsDestroyed;
            ShipLost = shipLost;
            ShipLossFee = shipLossFee;
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

        /// <summary>How an extracted run got out. Meaningless for a lost run.</summary>
        public ExtractionRoute Route { get; }

        /// <summary>Run length, in seconds.</summary>
        public float Duration { get; }

        /// <summary>The highest heat tier reached, or null when the scene has no heat tracker.</summary>
        public HeatTier? PeakHeat { get; }

        public int AuthorityShipsDestroyed { get; }

        /// <summary>True when the run ship was destroyed. A run lost on foot with the ship intact is not a ship loss.</summary>
        public bool ShipLost { get; }

        /// <summary>Replacement fee charged for a lost ship, in credits. Zero when the player could not pay.</summary>
        public int ShipLossFee { get; }

        /// <summary>The hold's contents when the run ended: banked on extraction, gone on a loss.</summary>
        public IReadOnlyList<CargoStack> Cargo => cargo;

        /// <summary>Total cargo mass, in tonnes.</summary>
        public float CargoMass { get; }

        /// <summary>Total cargo value, in credits.</summary>
        public int CargoValue { get; }
    }
}
