using SnpEvolution.Model;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Tasks;
using static SnpEvolution.Tests.Fixtures.Runs;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Specs.Tasks
{
    public class AcceptorTaskTests
    {
        private static TrialResult Halts(bool halts) => new TrialResult(Array.Empty<int>(), halts, TrialCoverage.Exact);

        [Fact]
        public void AcceptorScoreIsBalancedAccuracy()
        {
            AcceptorTask task = AcceptorTask.Of("even", n => n % 2 == 0, new[] { 1, 2, 3, 4, 5, 6 });

            Assert.Equal(1f, task.Score(new[] { false, true, false, true, false, true }.Select(Halts).ToList()));
            Assert.Equal(0.5f, task.Score(Enumerable.Repeat(Halts(true), 6).ToList()));
            Assert.Equal(0.5f, task.Score(Enumerable.Repeat(Halts(false), 6).ToList()));
            Assert.Equal(0.5f, AcceptorTask.Of("big", n => n >= 4, new[] { 1, 2, 3, 4 }).Score(Enumerable.Repeat(Halts(true), 4).ToList()));
            Assert.Equal(Readout.Halting, task.Cases[0].Readout);
        }

        [Fact]
        public void AcceptorThatAlwaysHaltsAcceptsEverything()
        {
            var network = new Network(new[] { InputNeuron(Array.Empty<int>(), StandardForget("a", 1)), OutputNeuron(0, Standard("a", 1)) });

            FitnessResult result = Runs.Evaluate(AcceptorTask.Of("big", n => n >= 3, Enumerable.Range(1, 4)), network, options: TaskOptions, solvedRetestCount: 3);

            Assert.Equal(0.5f, result.Fitness);
            Assert.Equal("accepts {1,2,3,4}", result.Description);
        }
    }
}
