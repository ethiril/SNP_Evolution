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

    // A way to find candidates for a task, such as a genetic algorithm, a shrink, or a search over programs. A search
    // holds only how it searches; everything about one run is in the request, so one instance serves every run.
    public interface ISearch<TCandidate> : ISearch
        where TCandidate : class
    {
        SearchOutcome<TCandidate> Run(SearchRequest<TCandidate> request);
    }

    // One run of a search: the task (a contract is a ContractTask), the budget every evaluation is charged to, the run's
    // random source and where progress goes. Seeds are candidates to start from; Stall is how to react when the best
    // stops improving, for searches that do; MaxGenerations and Cancellation stop it early. Networks is how network
    // searches make, edit and score networks.
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
