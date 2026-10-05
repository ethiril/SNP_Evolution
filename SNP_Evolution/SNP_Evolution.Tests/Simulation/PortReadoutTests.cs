using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Simulation
{
    public class PortReadoutTests
    {
        private static readonly SimulationOptions Options = new SimulationOptions(MaxSteps: 20, Repetitions: 3);

        // Neuron 1 starts the spike round: it sends one spike to 2 on step 0, 2 sends two to 3 on step 1, and 3 sends
        // one back to 2 on step 2, which sends two to 3 again on step 3, and so on for ever.
        private static Network Loop() => new Network(new[]
        {
            Neuron(1, new[] { 2 }, Standard("a", 1)),
            Neuron(0, new[] { 3 }, Standard("a", 1, produce: 2)),
            Neuron(0, new[] { 2 }, Standard("aa", 2)),
        });

        // Watching 2 and 3, with 3 as done: done fires on step 2, so steps 0 to 3 run and 3 still holds the two spikes 2
        // sent it on step 3.
        private static Trial LoopTrial() => new Trial(Loop(), InputSpikes.None, Readout.Ports, new PortWatch(new[] { 2, 3 }, new[] { 3 }, StepsAfterDone: 1));

        public static TheoryData<ISimulationEngine> Engines =>
            new TheoryData<ISimulationEngine> { new SequentialCpuEngine(), new ParallelCpuEngine(), new ExhaustiveCpuEngine() };

        [Theory]
        [MemberData(nameof(Engines))]
        public void ReportsEachWatchedNeuronsFiringsAndTheFinalSpikes(ISimulationEngine engine)
        {
            TrialResult result = engine.Run(new[] { LoopTrial() }, Options, new Random(1))[0];

            Assert.NotEmpty(result.PortRuns);
            Assert.All(result.PortRuns, run =>
            {
                Assert.Equal(new[] { new Firing(1, 2), new Firing(3, 2) }, run.Firings[0]);
                Assert.Equal(new[] { new Firing(2, 1) }, run.Firings[1]);
                Assert.Equal(new long[] { 0, 0, 2 }, run.FinalSpikes);
            });
        }

        [Fact]
        public void SamplingKeepsEveryRun()
        {
            TrialResult result = new SequentialCpuEngine().Run(new[] { LoopTrial() }, Options, new Random(1))[0];

            Assert.False(result.Exact);
            Assert.Equal(3, result.PortRuns.Count);
        }

        [Fact]
        public void RunsToMaxStepsWithoutDone()
        {
            var trial = LoopTrial() with { Watch = new PortWatch(new[] { 3 }, Array.Empty<int>(), StepsAfterDone: 0) };

            TrialResult result = new ExhaustiveCpuEngine().Run(new[] { trial }, Options with { MaxSteps = 7 }, new Random(1))[0];

            Assert.Equal(new[] { 2, 4, 6 }, result.PortRuns.Single().Firings[0].Select(firing => firing.Step));
        }

        [Fact]
        public void ExhaustiveEngineIsExactForADeterministicNetwork()
        {
            TrialResult result = new ExhaustiveCpuEngine().Run(new[] { LoopTrial() }, Options, new Random(1))[0];

            Assert.True(result.Exact);
            Assert.Single(result.PortRuns);
        }

        // One neuron may send one spike or two on step 0: two computations with different records.
        [Fact]
        public void ExhaustiveEngineReportsEachDistinctComputationOnce()
        {
            var network = new Network(new[] { Neuron(1, Array.Empty<int>(), Standard("a", 1), Standard("a", 1, produce: 2)) });
            var trial = new Trial(network, InputSpikes.None, Readout.Ports, new PortWatch(new[] { 1 }, new[] { 1 }, StepsAfterDone: 2));

            TrialResult result = new ExhaustiveCpuEngine().Run(new[] { trial }, Options, new Random(1))[0];

            Assert.True(result.Exact);
            Assert.Equal(new long[] { 1, 2 }, result.PortRuns.Select(run => run.Firings[0].Single().Spikes).OrderBy(spikes => spikes));
        }

        // Each step the neuron sends one spike or two, so the records double every step and soon outgrow the limit.
        [Fact]
        public void ExhaustiveEngineSamplesATooWideNetworkAndSaysSo()
        {
            var network = new Network(new[] { Neuron(30, Array.Empty<int>(), Standard("a+", 1), Standard("a+", 1, produce: 2)) });
            var trial = new Trial(network, InputSpikes.None, Readout.Ports, new PortWatch(new[] { 1 }, Array.Empty<int>(), StepsAfterDone: 0));

            TrialResult result = new ExhaustiveCpuEngine(maxConfigurations: 16).Run(new[] { trial }, Options, new Random(1))[0];

            Assert.False(result.Exact);
            Assert.Equal(Options.Repetitions, result.PortRuns.Count);
        }

        [Fact]
        public void APortsReadoutWithoutAWatchWatchesNothing()
        {
            var trial = new Trial(Loop(), InputSpikes.None, Readout.Ports);

            TrialResult result = new ExhaustiveCpuEngine().Run(new[] { trial }, Options, new Random(1))[0];

            Assert.Empty(result.PortRuns.Single().Firings);
        }

        [Fact]
        public void OtherReadoutsHaveNoPortRuns()
        {
            TrialResult result = new ExhaustiveCpuEngine().Run(new[] { Trial.Generate(PingPong()) }, Options, new Random(1))[0];

            Assert.Empty(result.PortRuns);
        }

        [MetalFact]
        public void MetalEngineRunsPortsReadoutsOnTheCpu()
        {
            var trials = Enumerable.Repeat(LoopTrial(), 50).ToList();

            IReadOnlyList<TrialResult> results = new MetalEngine(gpuThreshold: 0).Run(trials, Options, new Random(1));

            Assert.All(results, result => Assert.All(result.PortRuns, run => Assert.Equal(new[] { new Firing(2, 1) }, run.Firings[1])));
        }
    }
}
