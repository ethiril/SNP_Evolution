using System;
using System.Collections.Generic;

namespace SnpEvolution.Specs.Accounting
{
    public enum SearchStop
    {
        Solved,
        BudgetSpent,
        Stalled,
        Cancelled,
    }

    // Best is null only when the search never scored anything.
    public sealed record SearchOutcome<TCandidate>(SearchStop Stop, TCandidate? Best, float Fitness, string Description, BudgetReport Spent)
        where TCandidate : class
    {
        public bool Solved => Stop == SearchStop.Solved;

        public int Generations { get; init; }

        public IReadOnlyList<IReadOnlyList<float>> History { get; init; } = Array.Empty<IReadOnlyList<float>>();
    }
}
