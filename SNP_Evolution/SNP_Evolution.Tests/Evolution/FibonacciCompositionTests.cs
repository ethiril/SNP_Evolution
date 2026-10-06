using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using Xunit.Abstractions;

namespace SnpEvolution.Tests.Evolution
{
    // The Fibonacci register program built by hand from verified parts, typed wires and glue neurons, to show the
    // composition representation can express a growing sequence, and to give composition search a size to beat. It is
    // a test only: a Fibonacci network in the app would be a seed by another name.
    //
    // Two banks take turns, each a register X for the smaller value and an add part Y for the larger. A round starts
    // both parts of one bank and drains them into the other: Y's count goes to the other X and to the other Y's a port,
    // X's goes to the other Y's b port, so the other bank ends up holding B and A + B. Y's done starts the next round
    // and fires the output, so a round is one output gap.
    //
    // A part's start passes through its start neuron, its done neuron and the done of the round before, so a round
    // that drains y spikes lasts y + 3 steps. The banks therefore hold A - 2 and B - 3, and the done that starts a round
    // also reaches, through one relay, the three count ports of the bank being loaded, putting back the 1 spike X
    // lacks and the 2 Y lacks. The first three gaps (1, 1, 2) are shorter than a round can be; a preloaded glue
    // neuron fires them and then starts the first round, with both banks empty, which makes the gap of 3.
    public class FibonacciCompositionTests
    {
        private static readonly int[] Fibonacci = { 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987, 1597, 2584 };

        private readonly ITestOutputHelper output;

        public FibonacciCompositionTests(ITestOutputHelper output) => this.output = output;

        private sealed record Machine(Network Network, ModuleLibrary Library, PartCopy X1, PartCopy Y1, PartCopy X2, PartCopy Y2, int Output);

        private static LibraryPart Verified(Part part)
        {
            PartMeasurement measurement = PartEvolution.Measure(part);
            Assert.True(measurement.MeetsContract, measurement.Description);
            return LibraryPart.Of(part, measurement, new PartOrigin(1, "hand-built", 0));
        }

        // The hand-built register is checked against the catalogue's register contract, as a library part would be.
        private static Part Register() => ReferenceParts.Register(FirstParts.Larger) with { Contract = FirstParts.Named("register") };

        private static PartCopy Place(List<Neuron> neurons, Module module, int instance)
        {
            int offset = neurons.Count;
            var tag = new ModuleTag(module.Id, instance);
            neurons.AddRange(module.Body.Neurons.Select(neuron => neuron.WithConnections(neuron.Connections.Select(target => target + offset)).WithModule(tag)));
            return new PartCopy(tag, module.Part!, Enumerable.Range(offset + 1, module.Body.Neurons.Count).ToList());
        }

        private static void Connect(List<Neuron> neurons, int from, params int[] to) =>
            neurons[from - 1] = neurons[from - 1].WithConnections(neurons[from - 1].Connections.Concat(to));

        private static Machine Build()
        {
            var library = new ModuleLibrary();
            Module register = library.AddPart(Verified(Register()), "hand-built");
            Module add = library.AddPart(Verified(ReferenceParts.Add()), "hand-built");
            var neurons = new List<Neuron>();
            PartCopy x1 = Place(neurons, register, 1), y1 = Place(neurons, add, 2), x2 = Place(neurons, register, 3), y2 = Place(neurons, add, 4);
            int output = neurons.Count + 1, preamble = output + 1, first = output + 2, relay1 = output + 3, relay2 = output + 4;
            // The output fires on single spikes and drops the preamble's closing pair, which only the first-round trigger takes.
            neurons.Add(new Neuron(new[] { Rule.Standard("a", 1), Rule.Forget("aa", 2) }, 0, new int[0], isOutput: true));
            // 2 * 3 + 1 spikes: one spike on each of three steps, then a pair, as a register store drains.
            neurons.Add(new Neuron(new[] { Rule.Standard("a(aa)+", 2), Rule.Standard("a", 1, 2) }, 7, new[] { output, first }, false));
            neurons.Add(new Neuron(new[] { Rule.Forget("a", 1), Rule.Standard("aa", 2) }, 0, new[] { x1["start"], y1["start"], output, relay2 }, false));
            neurons.Add(new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { x1["n"], y1["a"], y1["b"] }, false));
            neurons.Add(new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { x2["n"], y2["a"], y2["b"] }, false));
            foreach ((PartCopy x, PartCopy y, PartCopy nextX, PartCopy nextY, int relay) in new[] { (x1, y1, x2, y2, relay1), (x2, y2, x1, y1, relay2) })
            {
                Connect(neurons, y["sum"], nextX["n"], nextY["a"]);
                Connect(neurons, x["out"], nextY["b"]);
                Connect(neurons, y["done"], nextX["start"], nextY["start"], output, relay);
            }
            return new Machine(new Network(neurons), library, x1, y1, x2, y2, output);
        }

        private static string WireText(Machine machine, Wire wire)
        {
            string Name(PartCopy copy) => copy.Tag == machine.X1.Tag ? "X1" : copy.Tag == machine.Y1.Tag ? "Y1" : copy.Tag == machine.X2.Tag ? "X2" : "Y2";
            return $"{Name(wire.From.Copy)}.{wire.From.Port.Name}>{Name(wire.To.Copy)}.{wire.To.Port.Name}";
        }

        private static List<int> OutputSteps(Network network, int steps)
        {
            var simulation = new NetworkSimulation(CompiledNetwork.Of(network), new Random(1), InputSpikes.None, OutputTiming.Interval, recordSpikeTrain: true);
            for (int step = 0; step < steps; step++)
            {
                simulation.Step();
            }
            return simulation.OutputSpikeSteps.ToList();
        }

        [Fact]
        public void ItIsBuiltOnlyFromVerifiedLibraryPartsJoinedByTypedWiresAndGlue()
        {
            Machine machine = Build();

            IReadOnlyList<PartCopy> copies = PartWiring.Copies(machine.Network, machine.Library);
            Assert.Equal(new[] { "register", "add", "register", "add" }, copies.OrderBy(copy => copy.Tag.Instance).Select(copy => copy.Part.Contract.Name));
            Assert.Equal(
                new[]
                {
                    "X1.out>Y2.b", "X2.out>Y1.b",
                    "Y1.done>X2.start", "Y1.done>Y2.start", "Y1.sum>X2.n", "Y1.sum>Y2.a",
                    "Y2.done>X1.start", "Y2.done>Y1.start", "Y2.sum>X1.n", "Y2.sum>Y1.a",
                },
                PartWiring.Wires(machine.Network, machine.Library).Select(wire => WireText(machine, wire)).Order());
            // Every synapse from one copy into another is one of those wires; the rest touch glue.
            Dictionary<int, PartCopy> owner = copies.SelectMany(copy => copy.Positions.Select(position => (position, copy))).ToDictionary(pair => pair.position, pair => pair.copy);
            int betweenCopies = machine.Network.Neurons
                .Select((neuron, index) => (neuron, position: index + 1))
                .Where(pair => owner.ContainsKey(pair.position))
                .Sum(pair => pair.neuron.Connections.Count(target => owner.TryGetValue(target, out PartCopy? to) && to != owner[pair.position]));
            Assert.Equal(10, betweenCopies);
            Assert.Equal(5, machine.Network.Neurons.Count(neuron => neuron.Module == null));
        }

        [Fact]
        public void EachRoundLastsExactlyItsGap()
        {
            Machine machine = Build();

            List<int> steps = OutputSteps(machine.Network, 300);

            List<int> gaps = steps.Zip(steps.Skip(1), (earlier, later) => later - earlier).ToList();
            Assert.Equal(Fibonacci.TakeWhile(gap => gap <= 89), gaps.Take(11));
            output.WriteLine("Output spikes on steps " + string.Join(", ", steps));
        }

        [Fact]
        public void ItMakesTheFirstSixteenFibonacciGapsExactlyOnTheExhaustiveEngine()
        {
            Network network = Build().Network;
            var task = new SequenceTask("Fibonacci", Fibonacci.Take(16).ToList());
            var evaluator = new FitnessEvaluator(new ExhaustiveCpuEngine(), task, new SimulationOptions(task.StepsNeeded, 20, OutputTiming.Interval), 1, new Random(1));

            FitnessResult result = evaluator.Evaluate(network);

            Assert.Equal(1f, result.Fitness);
            output.WriteLine(result.Description);
            output.WriteLine(HardwareCost.Of(network).ToString());
        }

        // Past the 16 it is scored on, the recurrence keeps going.
        [Fact]
        public void ItKeepsGoingPastTheTarget()
        {
            List<int> steps = OutputSteps(Build().Network, Fibonacci.Sum() + 10);

            Assert.Equal(Fibonacci, steps.Zip(steps.Skip(1), (earlier, later) => later - earlier).Take(Fibonacci.Length));
        }

        // The round whose gap is 3 (both banks empty) and the one whose gap is 5, for RESEARCH.md: what each neuron holds
        // at the start of each step, with * where it fires.
        [Fact]
        public void TraceTheRoundsOfGapThreeAndFive()
        {
            Machine machine = Build();
            Network network = machine.Network;
            var named = new List<(string Name, int Position)>
            {
                ("G", machine.Output + 2), ("O", machine.Output),
                ("Y1.start", machine.Y1["start"]), ("Y1.store", machine.Y1.Positions[5]), ("Y1.done", machine.Y1["done"]), ("R2", machine.Output + 4),
                ("X2.store", machine.X2.Positions[4]), ("Y2.store", machine.Y2.Positions[5]), ("Y2.sum", machine.Y2["sum"]), ("Y2.done", machine.Y2["done"]),
                ("R1", machine.Output + 3), ("X1.n", machine.X1["n"]), ("X1.store", machine.X1.Positions[4]),
            };
            var simulation = new NetworkSimulation(network, new Random(1));
            var held = new List<IReadOnlyList<long>>();
            output.WriteLine("| step | " + string.Join(" | ", named.Select(pair => pair.Name)) + " |");
            output.WriteLine("|---|" + string.Concat(named.Select(_ => "---|")));
            for (int step = 0; step <= 13; step++)
            {
                held.Add(simulation.Spikes);
                string Cell(int position)
                {
                    long spikes = held[step][position - 1];
                    bool fires = network.Neurons[position - 1].Rules.Any(rule => rule.Fire && rule.Applies(spikes));
                    return spikes == 0 ? "" : fires ? $"{spikes}*" : $"{spikes}";
                }
                if (step >= 4)
                {
                    output.WriteLine($"| {step} | " + string.Join(" | ", named.Select(pair => Cell(pair.Position))) + " |");
                }
                simulation.Step();
            }

            // When the gap of 5 starts (A = 3, B = 5), bank 2 holds A - 2 and B - 3, each spike stored as two.
            Assert.Equal(new[] { 2L, 4L }, new[] { machine.X2.Positions[4], machine.Y2.Positions[5] }.Select(position => held[8][position - 1]));
        }
    }
}
