using SnpEvolution.Search.Genome;
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
            library.AddPart(Verified(increment with { Network = increment.Network.WithNeuron(2, increment.Network.Neurons[2].WithRoles(true, false)) }), "a test");
            library.AddPart(Verified(ReferenceParts.Register()), "a test");
            library.AddPart(Verified(ReferenceParts.Add()), "a test");
            library.AddPart(Verified(ReferenceParts.Delay(2)), "a test");
            return library;
        }

        // Makes the glue neurons between parts: standard rules, up to MaxGlue of them.
        public static NetworkFactory GlueFactory(int inputCount, Random random) =>
            Factories.Networks(new GenomeSpace(InputCount: inputCount, RuleForm: RuleForm.Standard, MaxNeurons: MaxGlue), random);
    }
}
