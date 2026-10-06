using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Evolution
{
    public class PartWiringTests
    {
        private const int MaxNeurons = 24;

        private static LibraryPart Verified(Part part)
        {
            PartMeasurement measurement = PartEvolution.Measure(part);
            Assert.True(measurement.MeetsContract, measurement.Description);
            return LibraryPart.Of(part, measurement, new PartOrigin(1, "a test", 0));
        }

        // n + 2 on the same values the increment is checked on, with room for two increments in a row.
        private static Contract PlusTwo() => new Contract(
            "plus two",
            Port.In("start", PortKind.Trigger),
            new[] { Port.Out("done", PortKind.Trigger) },
            new[] { Port.In("n", PortKind.Count), Port.Out("out", PortKind.Count) },
            FirstParts.Values.Select(n => new ContractCase(new Dictionary<string, int> { ["n"] = n }, new Dictionary<string, int> { ["out"] = n + 2 }, "done")).ToList(),
            FirstParts.LatencyFor(2 * (FirstParts.Larger + 2)));

        // The first copy is the part's own network, so its in-ports are the network's inputs.
        private static Network Chained(ModuleLibrary library, Module module, int seed)
        {
            var first = new Network(module.Part!.Part.Network.Neurons.Select(neuron => neuron.WithModule(new ModuleTag(module.Id, 1))).ToList());
            return ModuleEdits.Insert(first, module, 2, MaxNeurons, library, new Random(seed));
        }

        // The plus-two contract read from the out and done ports of the copy put in last.
        private static PartMeasurement MeasurePlusTwo(Network network, ModuleLibrary library)
        {
            PartCopy last = PartWiring.Copies(network, library).OrderBy(copy => copy.Tag.Instance).Last();
            var binding = new PortBinding(new Dictionary<string, int> { ["out"] = last["out"], ["done"] = last["done"] });
            return PartEvolution.Measure(network, new ContractTask(PlusTwo(), binding));
        }

        private static NetworkFactory Factory(int maxNeurons, Random random) =>
            new NetworkFactory(new GenomeSpace(InputCount: 2, RuleForm: RuleForm.Standard, MaxNeurons: maxNeurons),
                new ExpressionGenerator(ExpressionGenerator.ExperimentalTemplates, 4, random), random);

        // Evolved parts carry the output role on a port, which the reference increment does not.
        private static Part IncrementWithOutput()
        {
            Part increment = ReferenceParts.Increment();
            return increment with { Network = increment.Network.WithNeuron(2, increment.Network.Neurons[2].WithRoles(true, false)) };
        }

        private static string WireText(Wire wire) => $"{wire.From.Copy.Tag.Instance}.{wire.From.Port.Name}>{wire.To.Copy.Tag.Instance}.{wire.To.Port.Name}";

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
                Assert.True(measurement.MeetsContract, measurement.Description);
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
        public void RewiringOnlyEverJoinsFittingPortsAndNeverFeedsAnInput()
        {
            var library = new ModuleLibrary();
            Module increment = library.AddPart(Verified(ReferenceParts.Increment()), "a test");
            Network chained = Chained(library, increment, 1);
            Network network = ModuleEdits.Insert(chained, increment, 3, MaxNeurons, library, new Random(1));
            var rewire = new RewirePort(library);
            var seen = new HashSet<string>();

            for (int seed = 0; seed < 40; seed++)
            {
                Network rewired = rewire.Mutate(network, new Random(seed));

                IReadOnlyList<PartCopy> copies = PartWiring.Copies(rewired, library);
                Assert.Equal(3, copies.Count);
                Dictionary<int, CopyPort> ports = copies.SelectMany(copy => copy.Ports).ToDictionary(port => port.Position);
                foreach (CopyPort from in ports.Values)
                {
                    foreach (int target in rewired.Neurons[from.Position - 1].Connections.Where(ports.ContainsKey).Where(target => ports[target].Copy != from.Copy))
                    {
                        Assert.True(PartWiring.Fits(from.Port, ports[target].Port), $"{from.Port.Name} > {ports[target].Port.Name}");
                    }
                }
                Assert.All(rewired.Neurons, neuron => Assert.DoesNotContain(neuron.Connections, target => rewired.Neurons[target - 1].IsInput));
                seen.Add(string.Join(" ", PartWiring.Wires(rewired, library).Select(WireText)));
            }
            Assert.True(seen.Count > 1);
        }

        [Fact]
        public void AGlueNeuronSitsOnAWireAndThePairStillAddsTwo()
        {
            var library = new ModuleLibrary();
            Module increment = library.AddPart(Verified(ReferenceParts.Increment()), "a test");
            Network network = Chained(library, increment, 1);
            var random = new Random(1);

            Network glued = new AddGlueNeuron(library, Factory(MaxNeurons, random)).Mutate(network, random);

            Assert.Equal(network.Neurons.Count + 1, glued.Neurons.Count);
            Assert.Single(PartWiring.Wires(glued, library));
            Assert.Null(glued.Neurons[^1].Module);
            PartMeasurement measurement = MeasurePlusTwo(glued, library);
            Assert.True(measurement.MeetsContract, measurement.Description);
            Assert.Same(network, new AddGlueNeuron(library, Factory(network.Neurons.Count, random)).Mutate(network, random));
        }

        [Fact]
        public void APartIsSwappedForACheaperOneWithTheSameContractAndKeepsItsWires()
        {
            var library = new ModuleLibrary();
            Module increment = library.AddPart(Verified(ReferenceParts.PaddedIncrement()), "a test");
            Network padded = Chained(library, increment, 1);
            library.AddPart(Verified(ReferenceParts.Increment()), "a cheaper test");
            var swap = new SwapPart(library);

            Network once = swap.Mutate(padded, new Random(1));
            Network twice = swap.Mutate(once, new Random(1));

            Assert.Equal(new[] { 12, 11, 10 }, new[] { padded, once, twice }.Select(network => network.Neurons.Count));
            Assert.Same(twice, swap.Mutate(twice, new Random(1)));
            Assert.Equal(new[] { "1.out>2.n", "1.done>2.start" }, PartWiring.Wires(twice, library).Select(WireText));
            Assert.Equal(new[] { true, true }, twice.Neurons.Take(2).Select(neuron => neuron.IsInput));
            PartMeasurement measurement = MeasurePlusTwo(twice, library);
            Assert.True(measurement.MeetsContract, measurement.Description);
            // A role on a neuron with no port of the same name would be lost, so that copy is not swapped.
            Network outputOnPadding = padded.WithNeuron(11, padded.Neurons[11].WithRoles(true, false));
            Assert.All(Enumerable.Range(0, 10), seed => Assert.Single(swap.Mutate(outputOnPadding, new Random(seed)).Neurons, neuron => neuron.IsOutput));
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

        [Fact]
        public void PartEditsAreOnlyOfferedWhenTheLibraryHoldsParts()
        {
            var harvested = new ModuleLibrary();
            harvested.Add(ModuleCuts.Whole(PingPong()), "a test");
            var withParts = new ModuleLibrary();
            withParts.AddPart(Verified(ReferenceParts.Increment()), "a test");
            NetworkFactory factory = Factory(MaxNeurons, new Random(1));

            IEnumerable<WeightedEdit> PartEdits(ModuleLibrary library) =>
                WeightedMutation.Structural(1, factory, modules: new ModuleSupport(library)).Edits.Where(edit => edit.Edit is RewirePort or AddGlueNeuron or SwapPart);

            Assert.Empty(PartEdits(harvested));
            Assert.Equal(3, PartEdits(withParts).Count());
        }
    }
}
