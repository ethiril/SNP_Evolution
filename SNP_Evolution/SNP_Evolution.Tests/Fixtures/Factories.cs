using SnpEvolution.Search.Genome;

namespace SnpEvolution.Tests.Fixtures
{
    internal static class Factories
    {
        public static NetworkFactory Networks(GenomeSpace space, Random random, IReadOnlyList<string>? templates = null, int maxSpikeGroupSize = 4) =>
            new NetworkFactory(space, new ExpressionGenerator(templates ?? ExpressionGenerator.ExperimentalTemplates, maxSpikeGroupSize, random), random);

        // Standard rules with the one simple template, the space the algorithm tests evolve in.
        public static NetworkFactory StandardRules(Random random) =>
            Networks(new GenomeSpace(RuleForm: RuleForm.Standard), random, ExpressionGenerator.SimpleTemplates);
    }
}
