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
    // was followed exhaustively, so the fitness is certain rather than sampled.
    public sealed record FitnessResult(float Fitness, IReadOnlyList<int> Outputs, string Description = "", bool Exact = false);

    // Scores a whole population in one call, so the simulation engine sees every network at once.
    public interface IPopulationEvaluator
    {
        IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks);
    }

    public sealed class FitnessEvaluator : IPopulationEvaluator
    {
        public const float SolvedThreshold = 0.985f;

        private readonly ISimulationEngine engine;
        private readonly SimulationOptions options;
        private readonly int solvedRetestCount;
        private readonly Random random;
        private long evaluations;

        public FitnessEvaluator(ISimulationEngine engine, ITask task, SimulationOptions options, int solvedRetestCount, Random random)
        {
            this.engine = engine;
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

        public IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks)
        {
            Interlocked.Add(ref evaluations, networks.Count);
            IReadOnlyList<TaskCase> cases = Task.Cases;
            var trials = networks.SelectMany(network => cases.Select(@case => new Trial(network, @case.Input, @case.Readout))).ToList();
            IReadOnlyList<TrialResult> results = engine.Run(trials, options, random);
            return Enumerable.Range(0, networks.Count).Select(index =>
            {
                List<TrialResult> own = results.Skip(index * cases.Count).Take(cases.Count).ToList();
                return new FitnessResult(Task.Score(own), own[0].Outputs, Task.Describe(own), own.All(result => result.Exact));
            }).ToList();
        }

        public FitnessResult Evaluate(Network network) => EvaluateAll(new[] { network })[0];

        // Sampled runs are stochastic, so one lucky score is not enough to stop the evolution; an exact one is.
        public bool IsReliablySolved(Network network)
        {
            FitnessResult first = Evaluate(network);
            if (!IsSolvingFitness(first.Fitness))
            {
                return false;
            }
            return first.Exact || Enumerable.Range(1, Math.Max(0, solvedRetestCount - 1)).All(_ => IsSolvingFitness(Evaluate(network).Fitness));
        }
    }
}
