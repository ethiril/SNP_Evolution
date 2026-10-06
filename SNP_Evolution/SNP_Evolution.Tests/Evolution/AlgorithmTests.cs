using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Evolution
{
    public class AlgorithmTests
    {
        private static readonly BenchmarkSettings Settings = BenchmarkSettings.Default with { Seeds = 1 };

        private static readonly BenchmarkTask Identity = TaskSuite.Functions.First(task => task.Name == "Compute n");

        private sealed class DelegateEvaluator : IPopulationEvaluator
        {
            private readonly Func<Network, float> fitness;

            public DelegateEvaluator(Func<Network, float> fitness) => this.fitness = fitness;

            public int Evaluated { get; private set; }

            public IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks)
            {
                Evaluated += networks.Count;
                return networks.Select(network => new FitnessResult(fitness(network), Array.Empty<int>())).ToList();
            }
        }

        private static NetworkFactory Factory(Random random) =>
            new NetworkFactory(new GenomeSpace(RuleForm: RuleForm.Standard), new ExpressionGenerator(ExpressionGenerator.SimpleTemplates, 4, random), random);

        // Rewards networks for having exactly three neurons, which every algorithm should manage.
        private static float ThreeNeurons(Network network) => 1f / (1 + Math.Abs(network.Neurons.Count - 3));

        public static TheoryData<string> StructuralAlgorithms => new TheoryData<string>(
            AlgorithmCatalog.All.Where(choice => !choice.Name.Contains("only")).Select(choice => choice.Name));

        [Theory]
        [Slow]
        [MemberData(nameof(StructuralAlgorithms))]
        public void StructuralAlgorithmsSolveTheIdentityFunction(string name)
        {
            AlgorithmChoice algorithm = AlgorithmCatalog.All.Single(choice => choice.Name == name);

            RunOutcome outcome = Benchmark.RunOnce(algorithm, Identity, seed: 1, budget: 4000, Settings);

            Assert.True(outcome.Solved, $"{name} reached {outcome.BestFitness}");
            Assert.True(outcome.Best!.Exact);
        }

        [Theory]
        [MemberData(nameof(StructuralAlgorithms))]
        public void BestNeverGetsWorseAndPopulationStaysFilled(string name)
        {
            var random = new Random(3);
            NetworkFactory factory = Factory(random);
            var evaluator = new DelegateEvaluator(ThreeNeurons);
            IGeneticAlgorithm algorithm = AlgorithmCatalog.All.Single(choice => choice.Name == name)
                .Create(new EvolutionContext(12, 0.5f, random, factory.NewNetwork, evaluator, factory, _ => { }));

            float best = float.MinValue;
            for (int generation = 0; generation < 15; generation++)
            {
                algorithm.NextGeneration();
                Assert.True(algorithm.Best!.Fitness >= best);
                best = algorithm.Best.Fitness;
                Assert.NotEmpty(algorithm.Population);
            }

            Assert.Equal(1f, best);
            Assert.Equal(16, algorithm.Generation);
        }

        [Fact]
        public void RankingPrefersTheSmallerOfEquallyFitNetworks()
        {
            var small = new Individual(Identity());
            var large = new Individual(ReferenceNetworks.NaturalNumbers());
            small.Record(new FitnessResult(0.5f, Array.Empty<int>()));
            large.Record(new FitnessResult(0.5f, Array.Empty<int>()));

            Assert.Same(small, Ranking.Rank(new[] { large, small })[0]);

            large.Record(new FitnessResult(0.6f, Array.Empty<int>()));
            Assert.Same(large, Ranking.Rank(new[] { small, large })[0]);
        }

        [Fact]
        public void MuPlusLambdaKeepsMuParents()
        {
            var random = new Random(0);
            NetworkFactory factory = Factory(random);
            var strategy = new MuPlusLambdaStrategy(3, 9, random, factory.NewNetwork, new DelegateEvaluator(ThreeNeurons), WeightedMutation.Structural(1, factory));

            strategy.NextGeneration();
            strategy.NextGeneration();

            Assert.Equal(3, strategy.Population.Count);
        }

        [Fact]
        public void MapElitesKeepsOneEliteForEachSize()
        {
            var random = new Random(0);
            NetworkFactory factory = Factory(random);
            var elites = new MapElites(10, random, factory.NewNetwork, new DelegateEvaluator(ThreeNeurons), new NeuronCrossover(), WeightedMutation.Structural(1, factory));

            for (int generation = 0; generation < 10; generation++)
            {
                elites.NextGeneration();
            }

            Assert.Equal(elites.Population.Count, elites.Population.Select(elite => MapElites.Cell(elite.Genes)).Distinct().Count());
            Assert.True(elites.Population.Count > 3);
        }

        [Fact]
        public void SpeciesDistanceIsZeroOnlyForTheSameStructure()
        {
            Assert.Equal(0, SpeciatedAlgorithm.Distance(Identity(), Identity()));
            Assert.True(SpeciatedAlgorithm.Distance(Identity(), ReferenceNetworks.NaturalNumbers()) > 2);
            Assert.Equal(SpeciatedAlgorithm.Distance(Identity(), AlwaysOutputsOne()), SpeciatedAlgorithm.Distance(AlwaysOutputsOne(), Identity()));
        }

        [Fact]
        public void BenchmarkSummarisesRunsPerAlgorithmAndTask()
        {
            var best = new Individual(Identity());
            RunOutcome[] outcomes =
            {
                new RunOutcome("A", "T", 1, true, 100, 1, best),
                new RunOutcome("A", "T", 2, true, 300, 1, best),
                new RunOutcome("A", "T", 3, false, 1000, 0.5f, null),
            };

            BenchmarkRow row = Assert.Single(Benchmark.Summarise(outcomes));

            Assert.Equal((3, 2, 200.0), (row.Runs, row.Solved, row.MedianEvaluationsToSolve!.Value));
            Assert.Equal(2.5 / 3, row.MeanBestFitness, precision: 5);
            Assert.Equal(Identity().Size, row.MeanSolvedSize);
            Assert.Contains("2/3", Benchmark.FormatTable(new[] { row }));
            Assert.StartsWith("task,algorithm", Benchmark.FormatCsv(new[] { row }));
        }

        [Fact]
        [Slow]
        public void SelectorHalvesTheCandidatesUntilOneIsLeft()
        {
            List<AlgorithmChoice> candidates = AlgorithmCatalog.All.Take(3).ToList();

            SelectionResult result = AlgorithmSelector.Select(candidates, TaskSuite.Generators[0], Settings, initialBudget: 60);

            Assert.Contains(result.Winner, candidates);
            Assert.Equal(new[] { 3, 2 }, result.Rounds.Select(round => round.Standings.Count));
            Assert.Equal(new[] { 60L, 120L }, result.Rounds.Select(round => round.Budget));
            Assert.NotNull(result.BestFound);
        }
    }
}
