using SnpEvolution.Model;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Verification;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;
using static SnpEvolution.Tests.Fixtures.PartWiringFixtures;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Specs.Parts
{
    public class PartWiringTests
    {
        // Evolved parts carry the output role on a port, which the reference increment does not.
        private static Part IncrementWithOutput()
        {
            Part increment = ReferenceParts.Increment();
            return increment with { Network = increment.Network.WithNeuron(2, increment.Network.Neurons[2].WithRoles(true, false)) };
        }

        [Fact]
        public void PartsShowTheirPortsAsNeuronPositions()
        {
            Part increment = ReferenceParts.Increment();

            Assert.Equal(new[] { "start@1", "n@2", "out@3", "done@4" }, increment.Ports().Select(port => $"{port.Port.Name}@{port.Position}"));
        }

        [Fact]
        public void OnlyPortsOfTheSameKindAndWidthJoinOutToIn()
        {
            Port start = Port.In("start", PortKind.Trigger), done = Port.Out("done", PortKind.Trigger);
            Port countIn = Port.In("n", PortKind.Count), countOut = Port.Out("out", PortKind.Count);

            Assert.True(PartWiring.Fits(done, start));
            Assert.True(PartWiring.Fits(countOut, countIn));
            Assert.False(PartWiring.Fits(start, done));
            Assert.False(PartWiring.Fits(countOut, start));
            Assert.False(PartWiring.Fits(Port.Out("out", PortKind.Interval), countIn));
            Assert.False(PartWiring.Fits(Port.Out("out", PortKind.Binary, 4), Port.In("n", PortKind.Binary, 5)));
            Assert.True(PartWiring.Fits(Port.Out("out", PortKind.Binary, 4), Port.In("n", PortKind.Binary, 4)));
        }

        [Fact]
        public void TwoVerifiedIncrementsWiredByTypeAddTwoWithNoEvolution()
        {
            var library = new ModuleLibrary();
            Module increment = library.AddPart(Verified(ReferenceParts.Increment()), "a test");

            for (int seed = 0; seed < 5; seed++)
            {
                Network network = Chained(library, increment, seed);

                Assert.Equal(new[] { "1.out>2.n", "1.done>2.start" }, PartWiring.Wires(network, library).Select(WireText));
                PartMeasurement measurement = MeasurePlusTwo(network, library);
                Assert.True(measurement.Verdict is Verdict.Passed, measurement.Description);
            }
        }

        [Fact]
        public void AThirdCopyIsFedByAnEarlierOneButNeverDoublesUpAnInPort()
        {
            var library = new ModuleLibrary();
            Module increment = library.AddPart(Verified(ReferenceParts.Increment()), "a test");

            for (int seed = 0; seed < 10; seed++)
            {
                Network network = ModuleEdits.Insert(Chained(library, increment, seed), increment, 3, MaxNeurons, library, new Random(seed));

                IReadOnlyList<Wire> wires = PartWiring.Wires(network, library);
                Assert.Equal(new[] { "n", "start" }, wires.Where(wire => wire.To.Copy.Tag.Instance == 3).Select(wire => wire.To.Port.Name).Order());
                Assert.All(wires.GroupBy(wire => wire.To.Position), group => Assert.Single(group));
            }
        }

        [Fact]
        public void APartInANetworkWithNoOtherPartIsWiredToUntypedNeurons()
        {
            var library = new ModuleLibrary();
            Module increment = library.AddPart(Verified(ReferenceParts.Increment()), "a test");

            Network network = ModuleEdits.Insert(PingPong(), increment, 1, MaxNeurons, library, new Random(1));

            PartCopy copy = PartWiring.Copies(network, library).Single();
            Assert.Contains(network.Neurons.Take(2), neuron => neuron.Connections.Contains(copy["start"]));
            Assert.Contains(network.Neurons.Take(2), neuron => neuron.Connections.Contains(copy["n"]));
            Assert.Empty(PartWiring.Wires(network, library));
        }

        [Fact]
        public void APartWhoseInsideWasEditedIsNoLongerTyped()
        {
            var library = new ModuleLibrary();
            Module increment = library.AddPart(Verified(ReferenceParts.Increment()), "a test");
            Network network = Chained(library, increment, 1);

            Network edited = network.WithNeuron(4, network.Neurons[4].WithRules(new[] { Standard("a", 1) }));

            Assert.Single(PartWiring.Copies(edited, library));
            Assert.Empty(PartWiring.Wires(edited, library));
        }

        [Fact]
        public void APortThatTakesTheOutputSendsNowhereAndAnOldOutputInACopyFeedsNoOtherPort()
        {
            var library = new ModuleLibrary();
            Module increment = library.AddPart(Verified(IncrementWithOutput()), "a test");
            int takeovers = 0;

            for (int seed = 0; seed < 20; seed++)
            {
                Network chained = Chained(library, increment, seed);
                Network hosted = ModuleEdits.Insert(PingPong(), increment, 1, MaxNeurons, library, new Random(seed));

                PartCopy first = PartWiring.Copies(chained, library).Single(copy => copy.Tag.Instance == 1);
                PartCopy second = PartWiring.Copies(chained, library).Single(copy => copy.Tag.Instance == 2);
                PartCopy guest = PartWiring.Copies(hosted, library).Single();
                if (chained.Neurons[second["out"] - 1].IsOutput)
                {
                    takeovers++;
                    Assert.DoesNotContain(second["out"], chained.Neurons[first["out"] - 1].Connections);
                }
                if (hosted.Neurons[guest["out"] - 1].IsOutput)
                {
                    Assert.Empty(hosted.Neurons[guest["out"] - 1].Connections);
                }
            }
            Assert.InRange(takeovers, 1, 19);
        }

    }
}
