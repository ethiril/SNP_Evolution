using System;
using System.Collections.Generic;

namespace SnpEvolution.Evolution.Accounting
{
    // Why a search stopped: it solved the task, spent its budget or ran out of generations, gave up after stalling, or
    // was cancelled.
    public enum SearchStop
    {
        Solved,
        BudgetSpent,
        Stalled,
        Cancelled,
    }

    // What every search returns: why it stopped, the best candidate it found with its fitness and a short account of
    // what that candidate does, and what the budget it was given had spent by then. Best is null only when the search
    // never scored anything. A search that runs in generations says how many, and the fitnesses each one scored.
    public sealed record SearchOutcome<TCandidate>(SearchStop Stop, TCandidate? Best, float Fitness, string Description, BudgetReport Spent)
        where TCandidate : class
    {
        public bool Solved => Stop == SearchStop.Solved;

        public int Generations { get; init; }

        public IReadOnlyList<IReadOnlyList<float>> History { get; init; } = Array.Empty<IReadOnlyList<float>>();
    }
}
