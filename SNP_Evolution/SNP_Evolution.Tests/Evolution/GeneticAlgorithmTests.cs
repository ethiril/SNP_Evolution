using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Genome;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Tests.Evolution
{
    public class GeneticAlgorithmTests
    {
        private static Network SingleRuleNetwork(string expression) => new Network(new[]
        {
            TestNetworks.OutputNeuron(1, new Rule(expression, 0, true)),
        });

        private static GeneticAlgorithm Create(
            int populationSize,
            Func<Network> createRandomNetwork,
            Func<Network, float> fitness,
            float mutationRate = 0,
            Func<string>? createRandomExpression = null,
            IParentSelection? selection = null) =>
            new GeneticAlgorithm(
                populationSize,
                new Random(0),
                createRandomNetwork,
                new DelegateEvaluator(network => new FitnessResult(fitness(network), new[] { 42 })),
                new GeneticOperators(
                    selection ?? new RouletteWheelSelection(),
                    new RuleExpressionCrossover(),
                    new RuleExpressionMutation(mutationRate, createRandomExpression ?? (() => "a"))),
                elitism: 1);

        private sealed class DelegateEvaluator : IPopulationEvaluator
        {
            private readonly Func<Network, FitnessResult> evaluate;

            public DelegateEvaluator(Func<Network, FitnessResult> evaluate) => this.evaluate = evaluate;

            public IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks) => networks.Select(evaluate).ToList();
        }

        [Fact]
        public void FirstGenerationReplacesNetworksWithoutAViableFitness()
        {
            var queuedNetworks = new Queue<Network>(new[] { SingleRuleNetwork("bad"), SingleRuleNetwork("good"), SingleRuleNetwork("good") });
            var algorithm = Create(2, queuedNetworks.Dequeue, network => network.Neurons[0].Rules[0].Expression == "good" ? 0.5f : 0f);

            algorithm.NextGeneration();

            Assert.Empty(queuedNetworks);
            Assert.Equal(0.5f, algorithm.Best?.Fitness);
            Assert.Equal(new[] { 42 }, algorithm.Best?.Outputs);
            Assert.Equal(2, algorithm.Generation);
        }

        [Fact]
        public void FittestIndividualSurvivesIntoTheNextGeneration()
        {
            Network fittest = SingleRuleNetwork("aa");
            var queuedNetworks = new Queue<Network>(new[] { SingleRuleNetwork("a"), fittest, SingleRuleNetwork("a") });
            var algorithm = Create(3, queuedNetworks.Dequeue, network => network == fittest ? 0.9f : 0.1f);

            algorithm.NextGeneration();

            Assert.Same(fittest, algorithm.Best?.Genes);
            Assert.Same(fittest, algorithm.Population[0].Genes);
        }

        [Fact]
        public void LaterGenerationsRecordOnlyInRangeFitnessAndPickBestFromThem()
        {
            var fitnessSequence = new Queue<float>(new[] { 0.5f, 0.5f, 0.4f, 1.5f });
            var algorithm = Create(2, () => SingleRuleNetwork("a"), _ => fitnessSequence.Dequeue());

            algorithm.NextGeneration();
            Assert.Empty(algorithm.FitnessHistory);

            algorithm.NextGeneration();
            Assert.Equal(new[] { 0.4f }, Assert.Single(algorithm.FitnessHistory));
            Assert.Equal(0.4f, algorithm.Best?.Fitness);
        }

        [Fact]
        public void MutationReplacesARuleExpression()
        {
            var algorithm = Create(2, () => SingleRuleNetwork("a"), _ => 0.5f, mutationRate: 1, createRandomExpression: () => "aaaa");

            algorithm.NextGeneration();

            Assert.Equal("aaaa", algorithm.Population[1].Genes.Neurons[0].Rules[0].Expression);
            Assert.Equal("a", algorithm.Population[0].Genes.Neurons[0].Rules[0].Expression);
        }

        [Fact]
        public void CrossoverMixesRuleExpressionsFromBothParents()
        {
            Network ManyRules(string expression) => new Network(new[]
            {
                TestNetworks.OutputNeuron(1, Enumerable.Range(0, 20).Select(_ => new Rule(expression, 0, true)).ToArray()),
            });
            int created = 0;
            var algorithm = Create(10, () => ManyRules(created++ % 2 == 0 ? "a" : "aa"), _ => 0.5f);

            algorithm.NextGeneration();

            Assert.Contains(algorithm.Population.Skip(1), child =>
                child.Genes.Neurons[0].Rules.Select(rule => rule.Expression).Distinct().Count() == 2);
        }

        [Fact]
        public void TournamentSelectionBreedsMostlyFromTheFittest()
        {
            int created = 0;
            var algorithm = Create(
                50,
                () => SingleRuleNetwork(created++ == 0 ? "aa" : "a"),
                network => network.Neurons[0].Rules[0].Expression == "aa" ? 0.9f : 0.1f,
                selection: new TournamentSelection(10));

            algorithm.NextGeneration();

            Assert.True(algorithm.Population.Count(individual => individual.Genes.Neurons[0].Rules[0].Expression == "aa") > 5);
        }

        [Fact]
        public void CrossoverToleratesParentsWithDifferentTopologies()
        {
            var topologyRandom = new Random(7);
            var expressions = new ExpressionGenerator(ExpressionGenerator.SimpleTemplates, 4, topologyRandom);
            var algorithm = Create(6, () => RandomTopology.Create(expressions, 4, topologyRandom), _ => 0.5f, mutationRate: 0.5f);

            for (int generation = 0; generation < 10; generation++)
            {
                algorithm.NextGeneration();
            }

            Assert.Equal(6, algorithm.Population.Count);
        }
    }
}
