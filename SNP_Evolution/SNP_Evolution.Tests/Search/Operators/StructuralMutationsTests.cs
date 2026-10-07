using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Search.Genome;
using SnpEvolution.Search.Operators;
using static SnpEvolution.Tests.Fixtures.TestNetworks;
using static SnpEvolution.Tests.Fixtures.WellFormedNetworks;

namespace SnpEvolution.Tests.Search.Operators
{
    public class StructuralMutationsTests
    {
        [Fact]
        public void EveryEditKeepsNetworksWellFormed()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                NetworkFactory factory = SmallFactory(seed, inputCount: seed % 3);
                WeightedMutation mutation = WeightedMutation.Structural(1, factory);
                var crossover = new NeuronCrossover();
                var random = new Random(seed);
                Network network = factory.NewNetwork();
                for (int step = 0; step < 200; step++)
                {
                    network = random.Next(5) == 0
                        ? crossover.Cross(network, factory.NewNetwork(), random)
                        : mutation.Edits[random.Next(mutation.Edits.Count)].Edit.Mutate(network, random);
                    AssertWellFormed(network, factory.Space);
                }
            }
        }

        [Fact]
        public void DuplicateNeuronCopiesANeuronAndItsIncomingSynapses()
        {
            NetworkFactory factory = SmallFactory(7, inputCount: 0);
            Network network = new Network(new[]
            {
                Neuron(2, new[] { 2 }, Standard("aa", 2)),
                Neuron(0, new[] { 3 }, Standard("a", 1)),
                OutputNeuron(0, Standard("a", 1)),
            });

            Network duplicated = Enumerable.Range(0, 20).Select(seed => new DuplicateNeuron(factory.Space).Mutate(network, new Random(seed)))
                .First(candidate => candidate.Neurons[^1].Rules[0].Expression == "a" && candidate.Neurons[^1].Connections.SequenceEqual(new[] { 3 }));

            Assert.Equal(4, duplicated.Neurons.Count);
            Assert.False(duplicated.Neurons[3].IsOutput);
            Assert.Equal(new[] { 2, 4 }, duplicated.Neurons[0].Connections);
            AssertWellFormed(duplicated, factory.Space with { InputCount = 0 });
        }

        [Fact]
        public void DuplicationIsOnlyOfferedWhenAllowed()
        {
            NetworkFactory allowed = SmallFactory(1);
            var notAllowed = Factories.Networks(allowed.Space with { DuplicateNeurons = false }, new Random(1), ExpressionGenerator.SimpleTemplates);

            Assert.Contains(WeightedMutation.Structural(1, allowed).Edits, edit => edit.Edit is DuplicateNeuron);
            Assert.DoesNotContain(WeightedMutation.Structural(1, notAllowed).Edits, edit => edit.Edit is DuplicateNeuron);
        }

        [Fact]
        public void NudgeExpressionChangesOneRunOfSpikesByOne()
        {
            Network network = new Network(new[] { OutputNeuron(0, new Rule("aa(aaa)*", 0, true)) });
            var seen = new HashSet<string>();

            for (int seed = 0; seed < 40; seed++)
            {
                seen.Add(new NudgeExpression().Mutate(network, new Random(seed)).Neurons[0].Rules[0].Expression);
            }

            Assert.Equal(new HashSet<string> { "a(aaa)*", "aaa(aaa)*", "aa(aa)*", "aa(aaaa)*" }, seen);
        }

        [Fact]
        public void SplitSynapseInsertsARelayInPlaceOfTheSynapse()
        {
            NetworkFactory factory = SmallFactory(0, inputCount: 0, form: RuleForm.Standard);

            Network split = new SplitSynapse(factory).Mutate(Identity(), new Random(0));

            Assert.Equal(3, split.Neurons.Count);
            Assert.Equal(new[] { 3 }, split.Neurons[0].Connections);
            Assert.Equal(new[] { 2 }, split.Neurons[2].Connections);
            Assert.Equal("a -> a", NetworkNotation.Rule(split.Neurons[2].Rules[0]));
        }
    }
}
