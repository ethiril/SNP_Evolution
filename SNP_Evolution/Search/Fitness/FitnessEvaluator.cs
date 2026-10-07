using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Model;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Search.Fitness
{
    // Outputs are the first case's, which for a generator is everything it produced. Exact is true when every case
    // was followed exhaustively, so the fitness is certain rather than sampled. Niche is the task's behaviour cell, and
    // Checks the score on each separate thing the task checks (see ITask.Checks).
    public sealed record FitnessResult(float Fitness, IReadOnlyList<int> Outputs, string Description = "", bool Exact = false, (int, int)? Niche = null,
        IReadOnlyList<float>? Checks = null);

    // Scores a whole population in one call, so the simulation engine sees every network at once.
    public interface IPopulationEvaluator
    {
        IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks);
    }

    // An evaluator that can say which task it scores on, which can change during a run.
    public interface ITaskEvaluator : IPopulationEvaluator
    {
        ITask Task { get; }
    }

    // Whether retests confirmed a solve; Failed is the first retest that did not solve the task, which the caller
    // should record as the network's score, since an elite kept with a lucky score is never rescored.
    public sealed record Confirmation(FitnessResult? Failed)
    {
        public bool Solved => Failed == null;
    }

    public sealed class FitnessEvaluator : ITaskEvaluator
    {
        private readonly ISimulationEngine engine;
        private readonly SimulationOptions options;
        private readonly int solvedRetestCount;
        private readonly Random random;
        private readonly EvaluationBudget budget;
        private readonly EvaluationSource source;
        // Whether each network scored so far had an exact result, by fingerprint, to count repeats.
        private readonly Dictionary<ulong, bool> scored = new Dictionary<ulong, bool>();

        // Every network scored is charged to the budget. Retests that confirm a solve are charged as verification
        // whatever source the evaluator was given.
        public FitnessEvaluator(ISimulationEngine engine, ITask task, SimulationOptions options, int solvedRetestCount, Random random,
            EvaluationBudget budget, EvaluationSource source = EvaluationSource.Main)
        {
            this.engine = engine;
            this.budget = budget;
            this.source = source;
            Task = task;
            this.options = options with { MaxSteps = Math.Max(options.MaxSteps, task.StepsNeeded) };
            this.solvedRetestCount = solvedRetestCount;
            this.random = random;
        }

        public FitnessEvaluator(ISimulationEngine engine, IFitnessFunction fitness, SimulationOptions options, int solvedRetestCount, Random random, EvaluationBudget budget)
            : this(engine, new GeneratorTask("Generator", Array.Empty<int>(), fitness), options, solvedRetestCount, random, budget)
        {
        }

        public ITask Task { get; }

        public IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks) => EvaluateAll(networks, source);

        private IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks, EvaluationSource spentOn)
        {
            budget.Charge(EvaluationKind.Network, networks.Count, spentOn);
            IReadOnlyList<TaskCase> cases = Task.Cases;
            var trials = networks.SelectMany(network => cases.Select(@case => new Trial(network, @case.Input, @case.Readout, @case.Watch))).ToList();
            IReadOnlyList<TrialResult> results = engine.Run(trials, options, random);
            List<FitnessResult> scores = Enumerable.Range(0, networks.Count).Select(index =>
            {
                List<TrialResult> own = results.Skip(index * cases.Count).Take(cases.Count).ToList();
                return new FitnessResult(Task.Score(own), own[0].Outputs, Task.Describe(own), own.All(result => result.Exact), Task.Niche(own), Task.Checks(own));
            }).ToList();
            // Retests repeat on purpose, so only the search's own evaluations are counted.
            if (spentOn != EvaluationSource.Verification)
            {
                CountRepeats(networks, scores);
            }
            return scores;
        }

        private void CountRepeats(IReadOnlyList<Network> networks, IReadOnlyList<FitnessResult> scores)
        {
            long repeats = 0;
            long exact = 0;
            lock (scored)
            {
                for (int index = 0; index < networks.Count; index++)
                {
                    ulong key = NetworkFingerprint.Of(networks[index]);
                    if (scored.TryGetValue(key, out bool wasExact))
                    {
                        repeats++;
                        exact += wasExact ? 1 : 0;
                    }
                    scored[key] = wasExact || scores[index].Exact;
                }
            }
            if (repeats > 0)
            {
                budget.CountRepeats(repeats, exact);
            }
        }

        public FitnessResult Evaluate(Network network) => EvaluateAll(new[] { network })[0];

        // Sampled runs are stochastic, so one lucky score is not enough to stop the evolution; an exact one is.
        public Confirmation ConfirmSolved(Network network) => new Confirmation(FailedRetest(network));

        // The first retest that does not solve the task, or null when they all do.
        private FitnessResult? FailedRetest(Network network)
        {
            FitnessResult Retest() => EvaluateAll(new[] { network }, EvaluationSource.Verification)[0];
            bool Solves(FitnessResult result) => Solved.Solves(result.Fitness, Task);
            FitnessResult first = Retest();
            if (!Solves(first))
            {
                return first;
            }
            if (first.Exact)
            {
                return null;
            }
            for (int retest = 1; retest < solvedRetestCount; retest++)
            {
                FitnessResult result = Retest();
                if (!Solves(result))
                {
                    return result;
                }
            }
            return null;
        }
    }
}
