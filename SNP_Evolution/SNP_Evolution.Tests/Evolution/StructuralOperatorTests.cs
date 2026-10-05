using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Evolution
{
    public class StructuralOperatorTests
    {
        private static NetworkFactory Factory(int seed, int inputCount = 1, RuleForm form = RuleForm.Mixed) =>
            new NetworkFactory(new GenomeSpace(InputCount: inputCount, RuleForm: form, MaxNeurons: 6, DuplicateNeurons: true),
                new ExpressionGenerator(ExpressionGenerator.ExperimentalTemplates, 4, new Random(seed)), new Random(seed));

        private static void AssertWellFormed(Network network, GenomeSpace space)
        {
            Assert.Single(network.Neurons, neuron => neuron.IsOutput);
            Assert.Equal(space.InputCount, network.Neurons.Count(neuron => neuron.IsInput));
            Assert.InRange(network.Neurons.Count, space.SmallestNetwork, space.MaxNeurons);
            for (int position = 1; position <= network.Neurons.Count; position++)
            {
                Neuron neuron = network.Neurons[position - 1];
                Assert.NotEmpty(neuron.Rules);
                Assert.InRange(neuron.Rules.Count, 1, space.MaxRulesPerNeuron);
                Assert.DoesNotContain(position, neuron.Connections);
                Assert.All(neuron.Connections, target => Assert.InRange(target, 1, network.Neurons.Count));
                Assert.Equal(neuron.Connections.Distinct().OrderBy(target => target), neuron.Connections);
                Assert.All(neuron.Rules.Where(rule => rule.IsStandard), rule => Assert.True(rule.Consume >= 1 && rule.Produce >= 1));
                Assert.True(neuron.InitialSpikes >= 0);
                Assert.True(!neuron.IsInput || neuron.InitialSpikes == 0);
            }
        }

        [Fact]
        public void RandomNetworksAreWellFormed()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                NetworkFactory factory = Factory(seed, inputCount: seed % 3);
                AssertWellFormed(factory.NewNetwork(), factory.Space);
            }
        }

        [Fact]
        public void EveryEditKeepsNetworksWellFormed()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                NetworkFactory factory = Factory(seed, inputCount: seed % 3);
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
            NetworkFactory factory = Factory(7, inputCount: 0);
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
            NetworkFactory allowed = Factory(1);
            var notAllowed = new NetworkFactory(allowed.Space with { DuplicateNeurons = false }, new ExpressionGenerator(ExpressionGenerator.SimpleTemplates, 4, new Random(1)), new Random(1));

            Assert.Contains(WeightedMutation.Structural(1, allowed).Edits, edit => edit.Edit is DuplicateNeuron);
            Assert.DoesNotContain(WeightedMutation.Structural(1, notAllowed).Edits, edit => edit.Edit is DuplicateNeuron);
        }

        [Fact]
        public void StandardSpaceOnlyMakesStandardRules()
        {
            NetworkFactory factory = Factory(1, form: RuleForm.Standard);

            Assert.All(Enumerable.Range(0, 50).Select(_ => factory.NewRule()), rule => Assert.True(rule.IsStandard));
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
            NetworkFactory factory = Factory(0, inputCount: 0, form: RuleForm.Standard);

            Network split = new SplitSynapse(factory).Mutate(Identity(), new Random(0));

            Assert.Equal(3, split.Neurons.Count);
            Assert.Equal(new[] { 3 }, split.Neurons[0].Connections);
            Assert.Equal(new[] { 2 }, split.Neurons[2].Connections);
            Assert.Equal("a -> a", NetworkNotation.Rule(split.Neurons[2].Rules[0]));
        }

        [Fact]
        public void RemoveNeuronRenumbersTheRemainingSynapses()
        {
            var network = new Network(new[]
            {
                Neuron(1, new[] { 2, 3 }, new Rule("a", 0, true)),
                Neuron(1, new[] { 3 }, new Rule("a", 0, true)),
                OutputNeuron(0, new Rule("a", 0, true)),
            });

            Network removed = NetworkEdits.RemoveNeuron(network, 1);

            Assert.Equal(2, removed.Neurons.Count);
            Assert.Equal(new[] { 2 }, removed.Neurons[0].Connections);
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
