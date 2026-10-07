using System;
using System.Collections.Generic;
using System.Threading;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Tasks;

namespace SnpEvolution.Evolution.Search
{
    public interface ISearch
    {
        string Name { get; }

        // A search that improves the candidates it is given, such as a shrink, cannot start from nothing.
        bool NeedsSeeds => false;
    }

    // A search holds only how it searches, so one registered instance serves every run.
    public interface ISearch<TCandidate> : ISearch
        where TCandidate : class
    {
        SearchOutcome<TCandidate> Run(SearchRequest<TCandidate> request);
    }

    // Networks is needed only by searches over networks; program searches ignore it.
    public sealed record SearchRequest<TCandidate>(ITask Task, EvaluationBudget Budget, Random Random, Action<string> Log)
        where TCandidate : class
    {
        public IReadOnlyList<TCandidate> Seeds { get; init; } = Array.Empty<TCandidate>();

        public StagnationPolicy? Stall { get; init; }

        public int MaxGenerations { get; init; } = int.MaxValue;

        public CancellationToken Cancellation { get; init; }

        public NetworkSetup? Networks { get; init; }

        public NetworkSetup RequireNetworks() => Networks ?? throw new ArgumentException("A network search needs a network setup.");
    }
}
