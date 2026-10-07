using SnpEvolution.Model;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.Fixtures.PortTrials;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Simulation
{
    public class PortReadoutTests
    {
        public static TheoryData<ISimulationEngine> Engines => new TheoryData<ISimulationEngine> { new SequentialCpuEngine(), new ParallelCpuEngine(), new ExhaustiveCpuEngine() };

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

        // Neuron 3 holds the two spikes 2 sends it, the most any neuron holds.
        [Theory]
        [MemberData(nameof(Engines))]
        public void RecordsTheMostSpikesAnyNeuronHeld(ISimulationEngine engine)
        {
            TrialResult result = engine.Run(new[] { LoopTrial() }, Options, new Random(1))[0];

            Assert.All(result.PortRuns, run => Assert.Equal(2, run.MostHeld));
        }

        // A shrunken network can lose the neuron a port is bound to; that port then never fires.
        [Theory]
        [MemberData(nameof(Engines))]
        public void APortPastTheLastNeuronNeverFires(ISimulationEngine engine)
        {
            var trial = new Trial(Loop(), InputSpikes.None, Readout.Ports, new PortWatch(new[] { 3, 5 }, new[] { 5 }, StepsAfterDone: 1));

            TrialResult result = engine.Run(new[] { trial }, Options, new Random(1))[0];

            Assert.All(result.PortRuns, run => Assert.Empty(run.Firings[1]));
        }

        // The done neuron fires on steps 0 to 4, but the run stops one step after the first of them.
        [Fact]
        public void TheRunStopsAfterTheFirstDoneNotTheLast()
        {
            var network = new Network(new[] { Neuron(5, Array.Empty<int>(), Standard("a+", 1)) });
            var trial = new Trial(network, InputSpikes.None, Readout.Ports, new PortWatch(new[] { 1 }, new[] { 1 }, StepsAfterDone: 1));

            TrialResult result = new ExhaustiveCpuEngine().Run(new[] { trial }, Options, new Random(1))[0];

            Assert.Equal(new[] { 0, 1 }, result.PortRuns.Single().Firings[0].Select(firing => firing.Step));
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
    }
}
