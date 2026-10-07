using SnpEvolution.Search.Genome;
using SnpEvolution.Search.Modules;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;

namespace SnpEvolution.Tests.Fixtures
{
    internal static class CompositionFixtures
    {
        // The most glue neurons a composition test lets the search add.
        public const int MaxGlue = 7;

        // Verified parts of every port kind the first parts use, one with its output role on a port.
        public static ModuleLibrary Library()
        {
            var library = new ModuleLibrary();
            Part increment = ReferenceParts.Increment();
            library.AddPart(PartFixtures.Verified(increment with { Network = increment.Network.WithNeuron(2, increment.Network.Neurons[2].WithRoles(true, false)) }), "a test");
            library.AddPart(PartFixtures.Verified(ReferenceParts.Register()), "a test");
            library.AddPart(PartFixtures.Verified(ReferenceParts.Add()), "a test");
            library.AddPart(PartFixtures.Verified(ReferenceParts.Delay(2)), "a test");
            return library;
        }

        // Makes the glue neurons between parts: standard rules, up to MaxGlue of them.
        public static NetworkFactory GlueFactory(int inputCount, Random random) =>
            Factories.Networks(new GenomeSpace(InputCount: inputCount, RuleForm: RuleForm.Standard, MaxNeurons: MaxGlue), random);

        public static (ModuleLibrary Library, Module Increment) Increments()
        {
            var library = new ModuleLibrary();
            return (library, library.AddPart(PartFixtures.Verified(ReferenceParts.Increment()), "a test"));
        }

        // Copies of a part with ports n, out, start and done, each draining into the next, read from the last.
        public static (Composition Composition, PortBinding Binding) ChainOfCopies(ModuleLibrary library, Module module, int copies)
        {
            Dictionary<string, int> ports = module.Part!.Part.Ports().ToDictionary(port => port.Port.Name, port => port.Position);
            var composition = new Composition(
                Enumerable.Range(1, copies).Select(instance => new PartInstance(instance, module.Id, module.Versions.Count - 1)).ToList(),
                Array.Empty<GlueNeuron>(),
                Enumerable.Range(1, copies - 1).SelectMany(instance => new[] { new PortWire(instance, "out", instance + 1, "n"), new PortWire(instance, "done", instance + 1, "start") }).ToList(),
                Array.Empty<Link>(),
                new[] { new Endpoint(1, ports["start"]), new Endpoint(1, ports["n"]) },
                Array.Empty<Endpoint>());
            IReadOnlyList<Endpoint> layout = composition.Layout(library);
            int Position(string port) => layout.ToList().IndexOf(new Endpoint(copies, ports[port])) + 1;
            return (composition, new PortBinding(new Dictionary<string, int> { ["out"] = Position("out"), ["done"] = Position("done") }));
        }
    }
}
