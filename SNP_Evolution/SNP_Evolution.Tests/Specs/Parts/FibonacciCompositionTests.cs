using SnpEvolution.Model;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using Xunit.Abstractions;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;

namespace SnpEvolution.Tests.Specs.Parts
{
    // Sequences.Fibonacci built by hand from verified parts, typed wires and glue, as a size for composition search to beat; it stays a test because in the app it would be a seed by another name.
    public class FibonacciCompositionTests
    {
        private readonly ITestOutputHelper output;

        public FibonacciCompositionTests(ITestOutputHelper output) => this.output = output;

        private sealed record Machine(Network Network, ModuleLibrary Library, PartCopy X1, PartCopy Y1, PartCopy X2, PartCopy Y2, int Output, int FirstRound, int Relay1, int Relay2);

        // The hand-built register is checked against the catalogue's register contract, as a library part would be.
        private static Part Register() => ReferenceParts.Register();

        private static PartCopy Place(List<Neuron> neurons, Module module, int instance)
        {
            int offset = neurons.Count;
            var tag = new ModuleTag(module.Id, instance);
            neurons.AddRange(module.Body.Neurons.Select(neuron => neuron.WithConnections(neuron.Connections.Select(target => target + offset)).WithModule(tag)));
            return new PartCopy(tag, module.Part!, Enumerable.Range(offset + 1, module.Body.Neurons.Count).ToList());
        }

        private static void Connect(List<Neuron> neurons, int from, params int[] to) =>
            neurons[from - 1] = neurons[from - 1].WithConnections(neurons[from - 1].Connections.Concat(to));

        // Both parts keep their store last.
        private static int Store(PartCopy copy) => copy.Positions[^1];

        private static Machine Build()
        {
            var library = new ModuleLibrary();
            Module register = library.AddPart(Verified(Register()), "hand-built");
            Module add = library.AddPart(Verified(ReferenceParts.Add()), "hand-built");
            var neurons = new List<Neuron>();
            PartCopy x1 = Place(neurons, register, 1), y1 = Place(neurons, add, 2), x2 = Place(neurons, register, 3), y2 = Place(neurons, add, 4);
            int output = neurons.Count + 1, firstRound = output + 2, relay1 = output + 3, relay2 = output + 4;
            // The output fires on single spikes and drops the preamble's closing pair, which only the first-round trigger takes.
            neurons.Add(new Neuron(new[] { Rule.Standard("a", 1), Rule.Forget("aa", 2) }, 0, new int[0], isOutput: true));
            // 2 * 3 + 1 spikes: one spike on each of three steps, then a pair, as a register store drains.
            neurons.Add(new Neuron(new[] { Rule.Standard("a(aa)+", 2), Rule.Standard("a", 1, 2) }, 7, new[] { output, firstRound }, false));
            neurons.Add(new Neuron(new[] { Rule.Forget("a", 1), Rule.Standard("aa", 2) }, 0, new[] { x1["start"], y1["start"], output, relay2 }, false));
            // A round lasts y + 3 steps, so the banks hold A - 2 and B - 3 and each relay puts back the 1 and 2 they lack.
            neurons.Add(new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { x1["n"], y1["a"], y1["b"] }, false));
            neurons.Add(new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { x2["n"], y2["a"], y2["b"] }, false));
            foreach ((PartCopy x, PartCopy y, PartCopy nextX, PartCopy nextY, int relay) in new[] { (x1, y1, x2, y2, relay1), (x2, y2, x1, y1, relay2) })
            {
                Connect(neurons, y["sum"], nextX["n"], nextY["a"]);
                Connect(neurons, x["out"], nextY["b"]);
                Connect(neurons, y["done"], nextX["start"], nextY["start"], output, relay);
            }
            return new Machine(new Network(neurons), library, x1, y1, x2, y2, output, firstRound, relay1, relay2);
        }

        private static string CopyNameWireText(Machine machine, Wire wire)
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
                PartWiring.Wires(machine.Network, machine.Library).Select(wire => CopyNameWireText(machine, wire)).Order());
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
        public void ItMakesTheFirstSixteenFibonacciGapsExactlyOnTheExhaustiveEngine()
        {
            Network network = Build().Network;
            var task = new SequenceTask("Sequences.Fibonacci", Sequences.Fibonacci.Take(16).ToList());
            var evaluator = new FitnessEvaluator(new ExhaustiveCpuEngine(), task, new SimulationOptions(task.StepsNeeded, 20, OutputTiming.Interval), 1, new Random(1), new EvaluationBudget());

            FitnessResult result = evaluator.Evaluate(network);

            Assert.Equal(1f, result.Fitness);
            output.WriteLine(result.Description);
            output.WriteLine(HardwareCost.Of(network).ToString());
        }

        [Fact]
        public void EachRoundLastsExactlyItsGapPastTheSixteenItIsScoredOn()
        {
            List<int> steps = OutputSteps(Build().Network, Sequences.Fibonacci.Sum() + 10);

            Assert.Equal(Sequences.Fibonacci, steps.Zip(steps.Skip(1), (earlier, later) => later - earlier).Take(Sequences.Fibonacci.Length));
            output.WriteLine("Output spikes on steps " + string.Join(", ", steps));
        }

        // Prints what each neuron holds at the start of each step, * where it fires, as the table RESEARCH.md shows.
        [Fact]
        public void TraceTheRoundsOfGapThreeAndFive()
        {
            Machine machine = Build();
            Network network = machine.Network;
            var named = new List<(string Name, int Position)>
            {
                ("G", machine.FirstRound), ("O", machine.Output),
                ("Y1.start", machine.Y1["start"]), ("Y1.store", Store(machine.Y1)), ("Y1.done", machine.Y1["done"]), ("R2", machine.Relay2),
                ("X2.store", Store(machine.X2)), ("Y2.store", Store(machine.Y2)), ("Y2.sum", machine.Y2["sum"]), ("Y2.done", machine.Y2["done"]),
                ("R1", machine.Relay1), ("X1.n", machine.X1["n"]), ("X1.store", Store(machine.X1)),
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
            Assert.Equal(new[] { 2L, 4L }, new[] { Store(machine.X2), Store(machine.Y2) }.Select(position => held[8][position - 1]));
        }
    }
}
