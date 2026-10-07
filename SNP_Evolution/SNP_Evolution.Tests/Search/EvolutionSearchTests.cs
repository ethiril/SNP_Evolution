using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Genome;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Tasks;
using static SnpEvolution.Tests.Fixtures.BenchmarkFixtures;
using static SnpEvolution.Tests.Fixtures.Runs;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search
{
    public class EvolutionSearchTests
    {
        private static readonly BenchmarkTask IdentityTask = TaskSuite.Functions.First(task => task.Name == "Compute n");

        [Fact]
        public void AnEvolutionSearchStartsFromItsSeed()
        {
            var random = new Random(4);
            NetworkFactory factory = Factories.StandardRules(random);
            var scoring = new NetworkScoring(() => new SequentialCpuEngine(), new SimulationOptions(20, 1, OutputTiming.Interval), 1);
            var setup = new NetworkSetup(4, 0.5f, factory, () => throw new InvalidOperationException("The seed should be used."), scoring);
            var seed = new Individual(Identity());

            SearchOutcome<Individual> outcome = SearchCatalog.StructuralDefault.Run(
                new SearchRequest<Individual>(FunctionTask.Of("n", n => n, new[] { 1, 2 }), new EvaluationBudget(), random, _ => { }) { Seeds = new[] { seed }, MaxGenerations = 1, Networks = setup });

            Assert.Same(seed.Genes, outcome.Best!.Genes);
        }

        public static TheoryData<string> StructuralAlgorithms => new TheoryData<string>(
            SearchCatalog.Evolution.Where(search => !search.EvolvesRulesOnly).Select(choice => choice.Name));

        [Theory]
        [Slow]
        [MemberData(nameof(StructuralAlgorithms))]
        public void StructuralAlgorithmsSolveTheIdentityFunction(string name)
        {
            EvolutionSearch algorithm = SearchCatalog.Evolution.Single(search => search.Name == name);

            RunOutcome outcome = Benchmark.RunOnce(algorithm, IdentityTask, seed: 1, budget: 4000, OneSeed);

            Assert.True(outcome.Solved, $"{name} reached {outcome.BestFitness}");
            Assert.True(outcome.Best!.Exact);
        }

        [Theory]
        [MemberData(nameof(StructuralAlgorithms))]
        public void BestNeverGetsWorseAndPopulationStaysFilled(string name)
        {
            var random = new Random(3);
            NetworkFactory factory = Factories.StandardRules(random);
            var evaluator = new RecordingEvaluator(ThreeNeurons);
            IGeneticAlgorithm algorithm = SearchCatalog.Evolution.Single(search => search.Name == name)
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
    }
}
