using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution
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

    public sealed class FitnessEvaluator : ITaskEvaluator
    {
        public const float SolvedThreshold = 0.985f;

        private readonly ISimulationEngine engine;
        private readonly SimulationOptions options;
        private readonly int solvedRetestCount;
        private readonly Random random;
        private readonly EvaluationCounter? counter;
        private readonly EvaluationSource source;
        private long evaluations;

        // Evaluations also go to the counter when there is one, as the source given, except retests that check a
        // network solves the task, which count as verification.
        public FitnessEvaluator(ISimulationEngine engine, ITask task, SimulationOptions options, int solvedRetestCount, Random random,
            EvaluationCounter? counter = null, EvaluationSource source = EvaluationSource.Main)
        {
            this.engine = engine;
            this.counter = counter;
            this.source = source;
            Task = task;
            this.options = options with { MaxSteps = Math.Max(options.MaxSteps, task.StepsNeeded) };
            this.solvedRetestCount = solvedRetestCount;
            this.random = random;
        }

        public FitnessEvaluator(ISimulationEngine engine, IFitnessFunction fitness, SimulationOptions options, int solvedRetestCount, Random random)
            : this(engine, new GeneratorTask("Generator", Array.Empty<int>(), fitness), options, solvedRetestCount, random)
        {
        }

        public ITask Task { get; }

        // How many networks have been scored, which is the budget algorithms are compared on.
        public long Evaluations => Interlocked.Read(ref evaluations);

        public static bool IsSolvingFitness(float fitness) => fitness >= SolvedThreshold && fitness <= 1;

        public IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks) => EvaluateAll(networks, source);

        private IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks, EvaluationSource spentOn)
        {
            Interlocked.Add(ref evaluations, networks.Count);
            counter?.Add(spentOn, networks.Count);
            IReadOnlyList<TaskCase> cases = Task.Cases;
            var trials = networks.SelectMany(network => cases.Select(@case => new Trial(network, @case.Input, @case.Readout, @case.Watch))).ToList();
            IReadOnlyList<TrialResult> results = engine.Run(trials, options, random);
            return Enumerable.Range(0, networks.Count).Select(index =>
            {
                List<TrialResult> own = results.Skip(index * cases.Count).Take(cases.Count).ToList();
                return new FitnessResult(Task.Score(own), own[0].Outputs, Task.Describe(own), own.All(result => result.Exact), Task.Niche(own), Task.Checks(own));
            }).ToList();
        }

        public FitnessResult Evaluate(Network network) => EvaluateAll(new[] { network })[0];

        // Sampled runs are stochastic, so one lucky score is not enough to stop the evolution; an exact one is.
        public bool IsReliablySolved(Network network)
        {
            FitnessResult Retest() => EvaluateAll(new[] { network }, EvaluationSource.Verification)[0];
            FitnessResult first = Retest();
            if (!IsSolvingFitness(first.Fitness))
            {
                return false;
            }
            return first.Exact || Enumerable.Range(1, Math.Max(0, solvedRetestCount - 1)).All(_ => IsSolvingFitness(Retest().Fitness));
        }
    }
}
