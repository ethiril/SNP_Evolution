using SnpEvolution.Evolution;
using SnpEvolution.Networks;

namespace SnpEvolution.Tests.Evolution
{
    public class FitnessEvaluatorTests
    {
        private static FitnessEvaluator EvaluatorExpecting(params int[] expectedSet) =>
            new FitnessEvaluator(expectedSet, maxSteps: 10, repetitions: 10, solvedRetestCount: 5, new Random(0));

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
                TestNetworks.Neuron("a", new[] { 2 }, new Rule("a", 1, true), new Rule("a", 0, false)),
                TestNetworks.OutputNeuron("a", new Rule("a", 0, true)),
            });
            var evaluator = new FitnessEvaluator(new[] { 1 }, maxSteps: 10, repetitions: 2, solvedRetestCount: 2, new ScriptedRandom(0, 0, 1, 1));

            Assert.False(evaluator.IsReliablySolved(network));
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
