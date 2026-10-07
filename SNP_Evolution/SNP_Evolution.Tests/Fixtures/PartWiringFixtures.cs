using SnpEvolution.Model;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Tests.Fixtures
{
    internal static class PartWiringFixtures
    {
        // Room for two increments and the glue between them.
        public const int MaxNeurons = 24;

        // n + 2 on the same values the increment is checked on, with room for two increments in a row.
        public static Contract PlusTwo() => new Contract(
            "plus two",
            Port.In("start", PortKind.Trigger),
            new[] { Port.Out("done", PortKind.Trigger) },
            new[] { Port.In("n", PortKind.Count), Port.Out("out", PortKind.Count) },
            FirstParts.Values.Select(n => new ContractCase(new Dictionary<string, int> { ["n"] = n }, new Dictionary<string, int> { ["out"] = n + 2 }, "done")).ToList(),
            Specifications.LatencyFor(2 * (FirstParts.Larger + 2)));

        // The first copy is the part's own network, so its in-ports are the network's inputs.
        public static Network Chained(ModuleLibrary library, Module module, int seed)
        {
            var first = new Network(module.Part!.Part.Network.Neurons.Select(neuron => neuron.WithModule(new ModuleTag(module.Id, 1))).ToList());
            return ModuleEdits.Insert(first, module, 2, MaxNeurons, library, new Random(seed));
        }

        // The plus-two contract read from the out and done ports of the copy put in last.
        public static PartMeasurement MeasurePlusTwo(Network network, ModuleLibrary library)
        {
            PartCopy last = PartWiring.Copies(network, library).OrderBy(copy => copy.Tag.Instance).Last();
            var binding = new PortBinding(new Dictionary<string, int> { ["out"] = last["out"], ["done"] = last["done"] });
            return new Verifier(new ContractTask(PlusTwo(), binding), new EvaluationBudget()).Measure(network);
        }

        public static string WireText(Wire wire) => $"{wire.From.Copy.Tag.Instance}.{wire.From.Port.Name}>{wire.To.Copy.Tag.Instance}.{wire.To.Port.Name}";
    }
}
