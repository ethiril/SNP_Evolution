using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Search.Genome;
using SnpEvolution.Search.Operators;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search
{
    public class WeightedMutationTests
    {
        [Fact]
        public void PressureMutatesEveryChild()
        {
            var random = new Random(3);
            NetworkFactory factory = Factories.StandardRules(random);
            Network network = factory.NewNetwork();

            Assert.Same(network, WeightedMutation.Structural(0, factory).Mutate(network, random));
            Assert.NotSame(network, WeightedMutation.Structural(0, factory, new MutationPressure { ExtraEdits = 3 }).Mutate(network, random));
        }

        [Fact]
        public void WeightedMutationRespectsItsRate()
        {
            Network network = Identity();
            var mutation = new WeightedMutation(0, new[] { new WeightedEdit("nudge", new NudgeExpression(), 1) });

            Assert.Same(network, mutation.Mutate(network, new Random(0)));
        }
    }
}
