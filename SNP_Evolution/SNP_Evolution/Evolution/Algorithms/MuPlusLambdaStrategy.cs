using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Algorithms
{
    // A (mu + lambda) evolution strategy: each generation every child is a mutated copy of a random parent, and the mu
    // best of parents and children survive. Children win ties, so the search can drift across equally fit networks.
    public sealed class MuPlusLambdaStrategy : IGeneticAlgorithm
    {
        private const int AttemptsToChange = 5;

        private readonly int mu;
        private readonly int lambda;
        private readonly Random random;
        private readonly Func<Network> createRandomNetwork;
        private readonly IPopulationEvaluator evaluator;
        private readonly IMutation mutation;
        private readonly ScoreHistory fitnessHistory = new ScoreHistory();
        private readonly Immigrants immigrants = new Immigrants();
        private List<Individual> parents = new List<Individual>();

        public MuPlusLambdaStrategy(int mu, int lambda, Random random, Func<Network> createRandomNetwork, IPopulationEvaluator evaluator, IMutation mutation)
        {
            this.mu = mu;
            this.lambda = lambda;
            this.random = random;
            this.createRandomNetwork = createRandomNetwork;
            this.evaluator = evaluator;
            this.mutation = mutation;
        }

        public IReadOnlyList<Individual> Population => parents;

        public int Generation { get; private set; } = 1;

        public Individual? Best { get; private set; }

        public IReadOnlyList<IReadOnlyList<float>> FitnessHistory => fitnessHistory.Rows;

        public void NextGeneration()
        {
            List<Individual> children = parents.Count == 0
                ? immigrants.Batch(Math.Max(mu, lambda), createRandomNetwork)
                : immigrants.Batch(lambda, () => Mutate(parents[random.Next(parents.Count)].Genes));
            Evaluation.Evaluate(evaluator, children);
            fitnessHistory.Record(children);
            parents = Ranking.Rank(children.Concat(parents)).Take(mu).ToList();
            Best = parents[0];
            Generation++;
        }

        // Newcomers take the place of children, so they must still beat the parents to survive.
        public void Immigrate(IReadOnlyList<Network> newcomers) => immigrants.Arrive(newcomers);

        public void Rescore()
        {
            if (parents.Count == 0)
            {
                return;
            }
            Evaluation.Evaluate(evaluator, parents);
            parents = Ranking.Rank(parents);
            Best = parents[0];
        }

        // A mutation can leave the network as it was, which would waste an evaluation, so it gets a few tries.
        private Network Mutate(Network parent)
        {
            Network child = parent;
            for (int attempt = 0; attempt < AttemptsToChange && ReferenceEquals(child, parent); attempt++)
            {
                child = mutation.Mutate(parent, random);
            }
            return child;
        }
    }
}
