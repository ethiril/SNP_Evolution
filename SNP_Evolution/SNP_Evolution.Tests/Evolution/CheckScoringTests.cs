using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.Evolution.ModuleFixtures;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Evolution
{
    public class CheckScoringTests
    {
        [Fact]
        public void SequenceChecksScoreEachGapInPlaceEvenAfterAMistake()
        {
            var task = new SequenceTask("test", new[] { 2, 1, 2 });
            var evaluator = new FitnessEvaluator(new SequentialCpuEngine(), task, new SimulationOptions(20, 2, OutputTiming.Interval), 1, new Random(1), new EvaluationBudget());

            FitnessResult result = evaluator.Evaluate(PingPong());

            Assert.Equal(new[] { 1f, 0f, 1f }, result.Checks);
            Assert.Equal("gap 2 (1)", task.CheckName(1));
            Assert.Equal(new[] { 2, 1, 2 }, ((SequenceTask)task.Focus(1)!).Expected);
        }

        [Fact]
        public void LexicasePicksSpecialistsButNeverANetworkThatIsBestAtNothing()
        {
            var random = new Random(1);
            List<Individual> ranked = Ranking.Rank(new[]
            {
                Scored(PingPong(), 0.6f, 0.6f, 0.6f),
                Scored(Identity(), 0.5f, 1f, 0f),
                Scored(NeverOutputs(), 0.5f, 0f, 1f),
                Scored(Chain(), 0.4f, 0.5f, 0.5f),
            });
            Func<Individual> pick = new LexicaseSelection().Prepare(ranked, random);

            List<Individual> picks = Enumerable.Range(0, 200).Select(_ => pick()).ToList();

            Assert.Contains(picks, individual => individual.Fitness == 0.5f && individual.Checks[1] == 1f);
            Assert.Contains(picks, individual => individual.Fitness == 0.5f && individual.Checks[0] == 1f);
            Assert.DoesNotContain(picks, individual => individual.Fitness == 0.4f);
        }

        [Fact]
        public void DiagnosisFindsTheFirstCheckNoNetworkDoes()
        {
            CheckDiagnosis diagnosis = CheckDiagnosis.Of(new[] { Scored(PingPong(), 0.5f, 1, 1, 0, 0), Scored(Identity(), 0.4f, 1, 0, 0.5f, 1) });

            Assert.Equal(new[] { 2 }, diagnosis.Unsolved);
            Assert.Equal(2, diagnosis.Frontier);
            Assert.StartsWith("No network yet does gap 3 (5)", diagnosis.Describe(new SequenceTask("fib", new[] { 1, 2, 5, 8 })));
        }

        [Fact]
        public void ATriggeredPartCountsItsGapsFromTheTrigger()
        {
            var task = (TriggeredSequenceTask)new SequenceTask("test", new[] { 1, 2, 2, 2, 7 }).Triggered(1)!;
            var evaluator = new FitnessEvaluator(new SequentialCpuEngine(), task, new SimulationOptions(30, 2, OutputTiming.Interval), 1, new Random(1), new EvaluationBudget());

            FitnessResult result = evaluator.Evaluate(TriggeredTwos());
            Cut cut = ModuleCuts.Whole(TriggeredTwos());

            Assert.Equal(new[] { 2, 2, 2 }, task.Expected);
            Assert.Equal(1, task.InputCount);
            Assert.Equal(new[] { 1f, 1f, 1f }, result.Checks);
            Assert.True(Solved.Solves(result.Fitness));
            Assert.Equal(3, cut.Body.Neurons.Count);
            Assert.Equal(new[] { 0 }, cut.Inputs);
        }
    }
}
