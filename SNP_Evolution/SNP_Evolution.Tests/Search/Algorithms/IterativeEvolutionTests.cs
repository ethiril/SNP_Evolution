using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Tasks;
using static SnpEvolution.Tests.Fixtures.GeneticAlgorithms;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Algorithms
{
    public class IterativeEvolutionTests
    {
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
                    return new FitnessEvaluator(new SequentialCpuEngine(), stageTask, new SimulationOptions(5, 3, OutputTiming.Interval), 2, random, new EvaluationBudget());
                },
                evaluator => Generational(6, random, PingPong, evaluator, WeightedMutation.Structural(0, Factories.StandardRules(random))),
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
            var algorithm = new StubAlgorithm();
            algorithm.Individuals.Add(lucky);
            var iterative = new IterativeEvolution(new SequenceTask("twos", Enumerable.Repeat(2, 4).ToList()), new[] { 2, 4 },
                stageTask => new FitnessEvaluator(new SequentialCpuEngine(), stageTask, new SimulationOptions(5, 3, OutputTiming.Interval), 2, new Random(0), new EvaluationBudget()),
                _ => algorithm,
                _ => { });

            iterative.NextGeneration();

            Assert.False(iterative.Stages.Single().Solved);
            Assert.True(lucky.Fitness < 1f);
        }
    }
}
