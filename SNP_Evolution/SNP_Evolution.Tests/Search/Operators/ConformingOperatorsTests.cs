using SnpEvolution.Model;
using SnpEvolution.Search.Genome;
using SnpEvolution.Search.Operators;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Operators
{
    // In a deterministic space an edit that gives a neuron rival rules is made again, and the parent is kept when no
    // attempt fits, rather than a rule being dropped.
    public class ConformingOperatorsTests
    {
        private static readonly GenomeSpace Deterministic = new GenomeSpace(RuleForm: RuleForm.Standard, Deterministic: true);

        private sealed class Edit(Func<Network, Network> edit) : IMutation
        {
            public int Calls { get; private set; }

            public Network Mutate(Network network, Random random)
            {
                Calls++;
                return edit(network);
            }
        }

        private static Network WithRivalRule(Network network) =>
            network.WithNeuron(0, network.Neurons[0].WithRules(network.Neurons[0].Rules.Append(Standard("a+", 1))));

        [Fact]
        public void KeepsTheParentWhenEveryAttemptGivesANeuronRivalRules()
        {
            Network parent = AlwaysOutputsOne();
            var edit = new Edit(WithRivalRule);

            Network child = new ConformingMutation(edit, Deterministic).Mutate(parent, new Random(0));

            Assert.Same(parent, child);
            Assert.Equal(ConformingMutation.Attempts, edit.Calls);
        }

        [Fact]
        public void TakesTheFirstAttemptThatFits()
        {
            Network parent = AlwaysOutputsOne(), fitting = PingPong();
            int call = 0;
            var alternating = new Edit(network => ++call < 3 ? WithRivalRule(network) : fitting);

            Assert.Same(fitting, new ConformingMutation(alternating, Deterministic).Mutate(parent, new Random(0)));
            Assert.Equal(3, alternating.Calls);
        }
    }
}
