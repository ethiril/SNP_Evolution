using SnpEvolution.Search.Genome;

namespace SnpEvolution.Tests.Fixtures
{
    internal static class Factories
    {
        // Makes networks in the space with rule expressions of up to length symbols, drawing both from random unless
        // the expressions are given their own source.
        public static NetworkFactory Networks(GenomeSpace space, Random random, IReadOnlyList<string>? templates = null, int length = 4, Random? expressions = null) =>
            new NetworkFactory(space, new ExpressionGenerator(templates ?? ExpressionGenerator.ExperimentalTemplates, length, expressions ?? random), random);

        // Standard rules with the one simple template, the space the algorithm tests evolve in.
        public static NetworkFactory StandardRules(Random random) =>
            Networks(new GenomeSpace(RuleForm: RuleForm.Standard), random, ExpressionGenerator.SimpleTemplates);
    }
}
