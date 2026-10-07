using SnpEvolution.Simulation;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Simulation
{
    public class RoutedEngineTests
    {
        private static readonly SimulationOptions Options = new SimulationOptions(20, 3, OutputTiming.Interval);

        // Runs no Ports readout, and remembers how many trials it was given.
        private sealed class NoPortsEngine : ISimulationEngine
        {
            public int Given { get; private set; }

            public EngineSupport Support => EngineSupport.Sampling with { Ports = false };

            public IReadOnlyList<TrialResult> Run(IReadOnlyList<Trial> trials, SimulationOptions options, Random random)
            {
                Given += trials.Count;
                return new SequentialCpuEngine().Run(trials, options, random);
            }
        }

        [Fact]
        public void EachTrialGoesToAnEngineThatSupportsIt()
        {
            var preferred = new NoPortsEngine();
            Trial[] trials = { Trial.Generate(AlwaysOutputsOne()), new Trial(AlwaysOutputsOne(), InputSpikes.None, Readout.Ports), Trial.Generate(PingPong()) };

            IReadOnlyList<TrialResult> results = new RoutedEngine(preferred, new ExhaustiveCpuEngine()).Run(trials, Options, new Random(1));

            Assert.Equal(2, preferred.Given);
            Assert.Equal(new[] { TrialCoverage.Sampled, TrialCoverage.Exact, TrialCoverage.Sampled }, results.Select(result => result.Coverage));
        }

        [Fact]
        public void ASmallBatchGoesWhollyToTheGeneralEngine()
        {
            var preferred = new NoPortsEngine();

            new RoutedEngine(preferred, new ExhaustiveCpuEngine(), minimumWork: 1_000).Run(new[] { Trial.Generate(AlwaysOutputsOne()) }, Options, new Random(1));

            Assert.Equal(0, preferred.Given);
        }
    }
}
