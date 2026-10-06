using SnpEvolution.Cli;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Genome;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Evolution
{
    public class SearchTests
    {
        private static NetworkFactory Factory(Random random) =>
            new NetworkFactory(new GenomeSpace(RuleForm: RuleForm.Standard), new ExpressionGenerator(ExpressionGenerator.SimpleTemplates, 4, random), random);

        private static GeneticAlgorithm Generational(int size, Random random, Func<Network> create, IPopulationEvaluator evaluator, IMutation mutation) =>
            new GeneticAlgorithm(size, random, create, evaluator, new GeneticOperators(new TournamentSelection(3), new NeuronCrossover(), mutation), 1, _ => { });

        [Fact]
        public void FindsTheTypoInAFibonacciTarget()
        {
            SuspectedTypo? typo = TargetPatterns.Find(new[] { 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 114, 233, 377, 610, 987 });

            Assert.NotNull(typo);
            Assert.Equal((11, 114, 144), (typo!.Index, typo.Found, typo.Expected));
        }

        [Theory]
        [InlineData(new[] { 2, 4, 6, 9, 10 }, 3, 8)]
        [InlineData(new[] { 1, 2, 4, 8, 15, 32 }, 4, 16)]
        [InlineData(new[] { 7, 1, 2, 3, 5, 8 }, 0, 1)]
        public void FindsASingleValueThatBreaksAPattern(int[] values, int index, int expected)
        {
            SuspectedTypo? typo = TargetPatterns.Find(values);

            Assert.Equal((index, expected), (typo!.Index, typo.Expected));
        }

        [Theory]
        [InlineData(new[] { 1, 1, 2, 3, 5, 8, 13 })]
        [InlineData(new[] { 3, 1, 4, 1, 5, 9 })]
        [InlineData(new[] { 1, 1, 2, 4 })]
        public void LeavesTargetsWithoutAClearTypoAlone(int[] values)
        {
            Assert.Null(TargetPatterns.Find(values));
        }

        [Fact]
        public void CurriculumStagesEndWithTheWholeTarget()
        {
            Assert.Equal(new[] { 3, 4, 5, 6 }, new CurriculumPlan(3, 1).Lengths(6));
            Assert.Equal(new[] { 8, 12, 15 }, new CurriculumPlan(8, 4).Lengths(15));
            Assert.Equal(new[] { 4 }, new CurriculumPlan(10, 2).Lengths(4));
        }

        [Fact]
        public void IterativeEvolutionSolvesEachStageInTurn()
        {
            var random = new Random(1);
            var task = new SequenceTask("twos", Enumerable.Repeat(2, 6).ToList());
            var seen = new List<int>();
            var iterative = new IterativeEvolution(task, new[] { 2, 4, 6 },
                stageTask =>
                {
                    seen.Add(((SequenceTask)stageTask).Expected.Count);
                    return new FitnessEvaluator(new SequentialCpuEngine(), stageTask, new SimulationOptions(5, 3, OutputTiming.Interval), 2, random);
                },
                evaluator => Generational(6, random, PingPong, evaluator, WeightedMutation.Structural(0, Factory(random))),
                _ => { });

            for (int generation = 0; generation < 10 && !iterative.IsComplete; generation++)
            {
                iterative.NextGeneration();
            }

            Assert.True(iterative.IsComplete);
            Assert.Equal(new[] { 2, 4, 6 }, seen);
            Assert.Equal(new[] { 2, 4, 6 }, iterative.Stages.Select(stage => stage.Length));
            Assert.All(iterative.Stages, stage => Assert.True(stage.Solved));
            Assert.Equal(4, iterative.Generation);
        }

        [Fact]
        public void ALuckyScoreDoesNotSolveAStageAndIsReplacedByItsFailedRetest()
        {
            var lucky = new Individual(AlwaysOutputsOne());
            lucky.Record(new FitnessResult(1f, new[] { 1 }));
            var algorithm = new ModularEvolutionTests.StubAlgorithm();
            algorithm.Individuals.Add(lucky);
            var iterative = new IterativeEvolution(new SequenceTask("twos", Enumerable.Repeat(2, 4).ToList()), new[] { 2, 4 },
                stageTask => new FitnessEvaluator(new SequentialCpuEngine(), stageTask, new SimulationOptions(5, 3, OutputTiming.Interval), 2, new Random(0)),
                _ => algorithm,
                _ => { });

            iterative.NextGeneration();

            Assert.False(iterative.Stages.Single().Solved);
            Assert.True(lucky.Fitness < 1f);
        }

        [Fact]
        public void StagnationEscalatesThenRestartsAndCalmsDownOnANewTask()
        {
            var random = new Random(2);
            NetworkFactory factory = Factory(random);
            var pressure = new MutationPressure();
            var evaluator = new RecordingEvaluator(_ => 0.5f);
            var recovery = new StagnationRecovery(
                Generational(8, random, factory.NewNetwork, evaluator, WeightedMutation.Structural(0.5f, factory, pressure)),
                new StagnationPolicy(Patience: 2, ImmigrantFraction: 0.25, MaxExtraEdits: 2), 8, pressure,
                factory.NewNetwork, WeightedMutation.Structural(1, factory), random, _ => { });

            recovery.NextGeneration();
            for (int generation = 0; generation < 4; generation++)
            {
                recovery.NextGeneration();
            }
            Assert.Equal((2, 0, 2), (recovery.Escalations, recovery.Restarts, pressure.ExtraEdits));

            recovery.NextGeneration();
            recovery.NextGeneration();
            Assert.Equal((1, 0), (recovery.Restarts, pressure.ExtraEdits));
            Assert.Equal(8, recovery.Population.Count);

            recovery.NextGeneration();
            recovery.NextGeneration();
            Assert.Equal(1, pressure.ExtraEdits);
            recovery.Rescore();
            Assert.Equal(0, pressure.ExtraEdits);
        }

        [Fact]
        public void PressureMutatesEveryChild()
        {
            var random = new Random(3);
            NetworkFactory factory = Factory(random);
            Network network = factory.NewNetwork();

            Assert.Same(network, WeightedMutation.Structural(0, factory).Mutate(network, random));
            Assert.NotSame(network, WeightedMutation.Structural(0, factory, new MutationPressure { ExtraEdits = 3 }).Mutate(network, random));
        }

        [Fact]
        public void GenerationalImmigrantsReplaceTheWeakestButNotTheElite()
        {
            var random = new Random(4);
            NetworkFactory factory = Factory(random);
            GeneticAlgorithm algorithm = Generational(6, random, factory.NewNetwork, new RecordingEvaluator(network => 1f / network.Neurons.Count), WeightedMutation.Structural(1, factory));
            algorithm.NextGeneration();
            Network elite = algorithm.Population[0].Genes;
            Network[] newcomers = { NeverOutputs(), NeverOutputs(), NeverOutputs() };

            algorithm.Immigrate(newcomers);

            Assert.Equal(6, algorithm.Population.Count);
            Assert.Same(elite, algorithm.Population[0].Genes);
            Assert.Equal(newcomers, algorithm.Population.Skip(3).Select(individual => individual.Genes));
        }

        [Fact]
        public void ArchiveAndStrategyImmigrantsJoinTheNextBatch()
        {
            var random = new Random(5);
            NetworkFactory factory = Factory(random);
            var evaluator = new RecordingEvaluator(network => 1f / network.Neurons.Count);
            var algorithms = new IGeneticAlgorithm[]
            {
                new MapElites(6, random, factory.NewNetwork, evaluator, new NeuronCrossover(), WeightedMutation.Structural(1, factory)),
                new MuPlusLambdaStrategy(2, 6, random, factory.NewNetwork, evaluator, WeightedMutation.Structural(1, factory)),
            };
            foreach (IGeneticAlgorithm algorithm in algorithms)
            {
                algorithm.NextGeneration();
                Network newcomer = NeverOutputs();
                algorithm.Immigrate(new[] { newcomer });
                evaluator.Seen.Clear();

                algorithm.NextGeneration();

                Assert.Contains(newcomer, evaluator.Seen);
                Assert.Equal(6, evaluator.Seen.Count);
            }
        }

        [Fact]
        public void RescoringTheArchiveUsesTheNewTask()
        {
            var random = new Random(6);
            NetworkFactory factory = Factory(random);
            var evaluator = new RecordingEvaluator(_ => 0.9f);
            var elites = new MapElites(8, random, factory.NewNetwork, evaluator, new NeuronCrossover(), WeightedMutation.Structural(1, factory));
            elites.NextGeneration();
            int cells = elites.Population.Count;

            evaluator.Fitness = _ => 0.2f;
            elites.Rescore();

            Assert.Equal(0.2f, elites.Best!.Fitness);
            Assert.Equal(cells, elites.Population.Count);
            Assert.All(elites.Population, elite => Assert.Equal(0.2f, elite.Fitness));
        }

        [Fact]
        public void BehaviourNichesReplaceSizeCellsWhenTheTaskHasThem()
        {
            var individual = new Individual(Identity());
            individual.Record(new FitnessResult(0.5f, Array.Empty<int>()));
            Assert.Equal((false, 2, 2), MapElites.CellOf(individual));

            individual.Record(new FitnessResult(0.5f, Array.Empty<int>(), Niche: (3, 4)));
            Assert.Equal((true, 3, 4), MapElites.CellOf(individual));
        }
    }
}
