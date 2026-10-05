using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution
{
    public sealed record FitnessResult(float Fitness, IReadOnlyList<int> Outputs);

    // Scores a whole population in one call, so the simulation engine sees every network at once.
    public interface IPopulationEvaluator
    {
        IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks);
    }

    public sealed class FitnessEvaluator : IPopulationEvaluator
    {
        public const float SolvedThreshold = 0.985f;

        private readonly ISimulationEngine engine;
        private readonly IFitnessFunction fitness;
        private readonly SimulationOptions options;
        private readonly int solvedRetestCount;
        private readonly Random random;

        public FitnessEvaluator(ISimulationEngine engine, IFitnessFunction fitness, SimulationOptions options, int solvedRetestCount, Random random)
        {
            this.engine = engine;
            this.fitness = fitness;
            this.options = options;
            this.solvedRetestCount = solvedRetestCount;
            this.random = random;
        }

        public static bool IsSolvingFitness(float fitness) => fitness >= SolvedThreshold && fitness <= 1;

        public IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks) =>
            engine.CollectOutputs(networks, options, random)
                .Select(outputs => new FitnessResult(fitness.Score(outputs), outputs))
                .ToList();

        public FitnessResult Evaluate(Network network) => EvaluateAll(new[] { network })[0];

        // Runs are stochastic, so one lucky score is not enough to stop the evolution.
        public bool IsReliablySolved(Network network) =>
            Enumerable.Range(0, solvedRetestCount).All(_ => IsSolvingFitness(Evaluate(network).Fitness));
    }
}
