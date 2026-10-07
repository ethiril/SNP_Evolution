using SnpEvolution.Search.Fitness;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Tasks;
using static SnpEvolution.Tests.Fixtures.TestNetworks;
using static SnpEvolution.Tests.Fixtures.TrialResults;

namespace SnpEvolution.Tests.Specs.Tasks
{
    public class SpikeWordTaskTests
    {
        [Fact]
        public void SpikeWordNicheIsTheRightPrefixBySpikeCount()
        {
            var task = new SpikeWordTask("w", new[] { true, false, true, true });

            // Fires on steps 0 and 2: right for three steps, with two spikes, as shares of 16 and 8 buckets.
            Assert.Equal((12, 4), task.Niche(new[] { Trains(new[] { 0, 2 }) }));
        }

        [Fact]
        public void BinaryWordScoreIsBalancedAccuracy()
        {
            var task = new SpikeWordTask("w", new[] { false, true, false, true });

            Assert.Equal(1f, task.Score(new[] { Trains(new[] { 1, 3, 9 }) }));
            Assert.Equal(0.5f, task.Score(new[] { Trains(Array.Empty<int>()) }));
            Assert.Equal(0.5f, task.Score(new[] { Trains(new[] { 0, 1, 2, 3 }) }));
        }

        [Fact]
        public void PingPongSpellsAlternatingBits()
        {
            FitnessResult result = Runs.Evaluate(new SpikeWordTask("01", Enumerable.Range(0, 12).Select(step => step % 2 == 1).ToList()), PingPong(), new ParallelCpuEngine(), new SimulationOptions(3, 5, OutputTiming.Interval), solvedRetestCount: 3);

            Assert.Equal(1f, result.Fitness);
            Assert.Equal("spikes 010101010101 / 010101010101", result.Description);
        }
    }
}
