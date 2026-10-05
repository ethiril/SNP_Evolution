using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution
{
    public sealed record FitnessResult(float Fitness, IReadOnlyList<int> Outputs);

    public sealed class FitnessEvaluator
    {
        public const float SolvedThreshold = 0.985f;

        private readonly IReadOnlyCollection<int> expectedSet;
        private readonly int maxSteps;
        private readonly int repetitions;
        private readonly int solvedRetestCount;
        private readonly Random random;

        public FitnessEvaluator(IReadOnlyCollection<int> expectedSet, int maxSteps, int repetitions, int solvedRetestCount, Random random)
        {
            this.expectedSet = expectedSet;
            this.maxSteps = maxSteps;
            this.repetitions = repetitions;
            this.solvedRetestCount = solvedRetestCount;
            this.random = random;
        }

        public static bool IsSolvingFitness(float fitness) => fitness >= SolvedThreshold && fitness <= 1;

        public FitnessResult Evaluate(Network network)
        {
            List<int> outputs = NetworkRunner.CollectOutputs(network, maxSteps, repetitions, random);
            return new FitnessResult(FitnessScore.Calculate(outputs, expectedSet), outputs);
        }

        // Runs are stochastic, so one lucky score is not enough to stop the evolution.
        public bool IsReliablySolved(Network network) =>
            Enumerable.Range(0, solvedRetestCount).All(_ => IsSolvingFitness(Evaluate(network).Fitness));
    }
}
