using SnpEvolution.Model;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Tasks;
using static SnpEvolution.Tests.Fixtures.Runs;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Fitness
{
    public class FitnessEvaluatorTests
    {
        private static FitnessEvaluator EvaluatorExpecting(params int[] expectedSet) =>
            Create(expectedSet, repetitions: 10, solvedRetestCount: 5, new Random(0));

        private static FitnessEvaluator Create(int[] expectedSet, int repetitions, int solvedRetestCount, Random random) =>
            new FitnessEvaluator(new SequentialCpuEngine(), new SetCoverageFitness(expectedSet), new SimulationOptions(MaxSteps: 10, repetitions), solvedRetestCount, random, new EvaluationBudget());

        // The feeder's first rule refills the output neuron and its second starves it, so the scripted choices pass one retest and fail the next.
        private static Network RefillThenStarve() => new Network(new[]
        {
            Neuron(1, new[] { 2 }, new Rule("a", 1, true), new Rule("a", 0, false)),
            OutputNeuron(1, new Rule("a", 0, true)),
        });

        // Scores every network the same, on the cases of a one-gap sequence.
        private class FixedScoreTask : ITask
        {
            private readonly ITask shape = new SequenceTask("fixed", new[] { 1 });
            private readonly float score;

            public FixedScoreTask(float score) => this.score = score;

            public string Name => "fixed";

            public int InputCount => shape.InputCount;

            public IReadOnlyList<TaskCase> Cases => shape.Cases;

            public float Score(IReadOnlyList<TrialResult> results) => score;

            public string Describe(IReadOnlyList<TrialResult> results) => "";
        }

        private sealed class FixedScoreContract : FixedScoreTask, ITask
        {
            public FixedScoreContract(float score) : base(score) { }

            public float SolvedFitness => 1f;
        }

        private sealed class ScriptedRandom : Random
        {
            private readonly Queue<int> choices;

            public ScriptedRandom(params int[] choices) => this.choices = new Queue<int>(choices);

            public override int Next(int maxValue) => maxValue == 1 ? 0 : choices.Dequeue();
        }

        [Fact]
        public void ATaskThatCountsOnlyAPerfectScoreRejectsOneJustShortOfIt()
        {
            FitnessEvaluator Evaluator(ITask task) => new FitnessEvaluator(new SequentialCpuEngine(), task, new SimulationOptions(MaxSteps: 10, 2), solvedRetestCount: 3, new Random(0), new EvaluationBudget());

            Assert.True(Evaluator(new FixedScoreTask(0.99f)).ConfirmSolved(AlwaysOutputsOne()).Solved);
            Assert.False(Evaluator(new FixedScoreContract(0.99f)).ConfirmSolved(AlwaysOutputsOne()).Solved);
        }

        [Fact]
        public void EvaluateReportsFitnessAndOutputs()
        {
            FitnessResult result = EvaluatorExpecting(1).Evaluate(AlwaysOutputsOne());

            Assert.Equal(1f, result.Fitness);
            Assert.Equal(Enumerable.Repeat(1, 10), result.Outputs);
        }

        [Fact]
        public void NetworkThatAlwaysHitsTheExpectedSetIsReliablySolved()
        {
            Assert.True(EvaluatorExpecting(1).ConfirmSolved(AlwaysOutputsOne()).Solved);
        }

        [Fact]
        public void NetworkMissingPartOfTheExpectedSetIsNotSolved()
        {
            Assert.False(EvaluatorExpecting(1, 2).ConfirmSolved(AlwaysOutputsOne()).Solved);
        }

        [Fact]
        public void OneFailedRetestMeansNotSolved()
        {
            var evaluator = Create(new[] { 1 }, repetitions: 2, solvedRetestCount: 2, new ScriptedRandom(0, 0, 1, 1));

            Assert.False(evaluator.ConfirmSolved(RefillThenStarve()).Solved);
        }

        [Fact]
        public void AFailedRetestReplacesALuckyScore()
        {
            // Its first retest misses, so the held score of 1 should not survive.
            var evaluator = Create(new[] { 1 }, repetitions: 2, solvedRetestCount: 2, new ScriptedRandom(1, 1));
            var lucky = new Individual(RefillThenStarve());
            lucky.Record(new FitnessResult(1f, new[] { 1, 1 }));

            Assert.False(SolveCheck.Confirms(lucky, evaluator));
            Assert.False(Solved.Solves(lucky.Fitness));
        }

        [Fact]
        public void AConfirmedSolveKeepsItsScore()
        {
            var solved = new Individual(AlwaysOutputsOne());
            solved.Record(new FitnessResult(1f, new[] { 1 }, "held"));

            Assert.True(SolveCheck.Confirms(solved, EvaluatorExpecting(1)));
            Assert.Equal("held", solved.Description);
        }

        [Fact]
        public void EvaluateAllScoresEachNetworkInOrder()
        {
            IReadOnlyList<FitnessResult> results = EvaluatorExpecting(1).EvaluateAll(new[] { NeverOutputs(), AlwaysOutputsOne() });

            Assert.Equal(new[] { 0f, 1f }, results.Select(result => result.Fitness));
        }

        [Fact]
        public void ExactResultIsReliablySolvedWithoutRetesting()
        {
            var budget = new EvaluationBudget();
            var evaluator = new FitnessEvaluator(new ExhaustiveCpuEngine(), FunctionTask.Of("n", n => n, new[] { 1, 2 }), TaskOptions, solvedRetestCount: 5, new Random(0), budget);

            Assert.True(evaluator.ConfirmSolved(Identity()).Solved);
            Assert.Equal(1, budget.Networks);
        }

        [Fact]
        public void RetestsOfASolvedNetworkCountAsVerification()
        {
            var counter = new EvaluationBudget();
            var evaluator = new FitnessEvaluator(new SequentialCpuEngine(), FunctionTask.Of("n", n => n, new[] { 1, 2 }),
                new SimulationOptions(40, 5, OutputTiming.Interval), 3, new Random(1), counter, EvaluationSource.SideRun);

            evaluator.Evaluate(Identity());
            Assert.True(evaluator.ConfirmSolved(Identity()).Solved);

            Assert.Equal(1, counter[EvaluationSource.SideRun]);
            Assert.Equal(3, counter[EvaluationSource.Verification]);
        }

        [Fact]
        public void ANetworkScoredAgainCountsAsARepeatAndExactlySoUnderTheExhaustiveEngine()
        {
            var budget = new EvaluationBudget();
            var evaluator = new FitnessEvaluator(new ExhaustiveCpuEngine(), FunctionTask.Of("n", n => n, new[] { 1, 2 }), TaskOptions, solvedRetestCount: 5, new Random(0), budget);

            evaluator.EvaluateAll(new[] { Identity(), Identity() });
            evaluator.Evaluate(Identity());
            evaluator.ConfirmSolved(Identity());

            Assert.Equal(2, budget.Report().Repeats);
            Assert.Equal(2, budget.Report().ExactRepeats);
        }

        [Fact]
        public void ASampledRepeatIsNotAnExactOne()
        {
            var budget = new EvaluationBudget();
            var evaluator = new FitnessEvaluator(new SequentialCpuEngine(), new SetCoverageFitness(new[] { 1 }), new SimulationOptions(MaxSteps: 10, 2), 1, new Random(0), budget);

            evaluator.EvaluateAll(new[] { RefillThenStarve(), RefillThenStarve(), AlwaysOutputsOne() });

            Assert.Equal(1, budget.Report().Repeats);
            Assert.Equal(0, budget.Report().ExactRepeats);
        }
    }
}
