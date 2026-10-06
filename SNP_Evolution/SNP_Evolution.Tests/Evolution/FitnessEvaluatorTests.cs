using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Tests.Evolution
{
    public class FitnessEvaluatorTests
    {
        private static FitnessEvaluator EvaluatorExpecting(params int[] expectedSet) =>
            Create(expectedSet, repetitions: 10, solvedRetestCount: 5, new Random(0));

        private static FitnessEvaluator Create(int[] expectedSet, int repetitions, int solvedRetestCount, Random random) =>
            new FitnessEvaluator(new SequentialCpuEngine(), new SetCoverageFitness(expectedSet), new SimulationOptions(MaxSteps: 10, repetitions), solvedRetestCount, random);

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

        [Fact]
        public void ATaskThatCountsOnlyAPerfectScoreRejectsOneJustShortOfIt()
        {
            FitnessEvaluator Evaluator(ITask task) => new FitnessEvaluator(new SequentialCpuEngine(), task, new SimulationOptions(MaxSteps: 10, 2), solvedRetestCount: 3, new Random(0));

            Assert.True(Evaluator(new FixedScoreTask(0.99f)).IsReliablySolved(TestNetworks.AlwaysOutputsOne()));
            Assert.False(Evaluator(new FixedScoreContract(0.99f)).IsReliablySolved(TestNetworks.AlwaysOutputsOne()));
        }

        [Fact]
        public void EvaluateReportsFitnessAndOutputs()
        {
            FitnessResult result = EvaluatorExpecting(1).Evaluate(TestNetworks.AlwaysOutputsOne());

            Assert.Equal(1f, result.Fitness);
            Assert.Equal(Enumerable.Repeat(1, 10), result.Outputs);
        }

        [Fact]
        public void NetworkThatAlwaysHitsTheExpectedSetIsReliablySolved()
        {
            Assert.True(EvaluatorExpecting(1).IsReliablySolved(TestNetworks.AlwaysOutputsOne()));
        }

        [Fact]
        public void NetworkMissingPartOfTheExpectedSetIsNotSolved()
        {
            Assert.False(EvaluatorExpecting(1, 2).IsReliablySolved(TestNetworks.AlwaysOutputsOne()));
        }

        [Fact]
        public void OneFailedRetestMeansNotSolved()
        {
            // The feeder's first rule refills the output neuron and its second starves it, so the scripted choices pass one retest and fail the next.
            var network = new Network(new[]
            {
                TestNetworks.Neuron(1, new[] { 2 }, new Rule("a", 1, true), new Rule("a", 0, false)),
                TestNetworks.OutputNeuron(1, new Rule("a", 0, true)),
            });
            var evaluator = Create(new[] { 1 }, repetitions: 2, solvedRetestCount: 2, new ScriptedRandom(0, 0, 1, 1));

            Assert.False(evaluator.IsReliablySolved(network));
        }

        [Fact]
        public void EvaluateAllScoresEachNetworkInOrder()
        {
            IReadOnlyList<FitnessResult> results = EvaluatorExpecting(1).EvaluateAll(new[] { TestNetworks.NeverOutputs(), TestNetworks.AlwaysOutputsOne() });

            Assert.Equal(new[] { 0f, 1f }, results.Select(result => result.Fitness));
        }

        [Theory]
        [InlineData(0.985f, true)]
        [InlineData(1f, true)]
        [InlineData(0.984f, false)]
        [InlineData(1.01f, false)]
        [InlineData(float.NaN, false)]
        public void SolvingFitnessIsBetweenTheThresholdAndOne(float fitness, bool solving)
        {
            Assert.Equal(solving, FitnessEvaluator.IsSolvingFitness(fitness));
        }

        private sealed class ScriptedRandom : Random
        {
            private readonly Queue<int> choices;

            public ScriptedRandom(params int[] choices) => this.choices = new Queue<int>(choices);

            public override int Next(int maxValue) => maxValue == 1 ? 0 : choices.Dequeue();
        }
    }
}
