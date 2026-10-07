using SnpEvolution.Model;
using SnpEvolution.Search.Modules;
using SnpEvolution.Search.Operators;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Specs.Parts
{
    public class ModuleEditTests
    {
        [Fact]
        public void InsertingAModuleWiresItsPortsAndTagsItsNeurons()
        {
            var library = new ModuleLibrary();
            Module module = library.Add(ModuleCuts.Cut(Chain(), new[] { 1, 2 }), "a test")!;

            for (int seed = 0; seed < 20; seed++)
            {
                Network inserted = ModuleEdits.Insert(PingPong(), module, 7, 10, library, new Random(seed));

                Assert.Equal(4, inserted.Neurons.Count);
                Assert.Single(inserted.Neurons, neuron => neuron.IsOutput);
                Assert.All(inserted.Neurons.Skip(2), neuron => Assert.Equal(new ModuleTag(module.Id, 7), neuron.Module));
                Assert.Contains(inserted.Neurons.Take(2), neuron => neuron.Connections.Contains(3));
                Assert.Equal(new[] { 4 }, inserted.Neurons[2].Connections);
            }
            Network full = PingPong();
            Assert.Same(full, ModuleEdits.Insert(full, module, 8, 3, library, new Random(1)));
        }

        [Fact]
        public void FrozenModulesRejectEditsToTheirInsidesButNotToTheirSpikes()
        {
            var library = new ModuleLibrary();
            Module module = ModuleOf(library, Chain());
            Network network = ModuleEdits.Insert(PingPong(), module, 1, 10, library, new Random(1));
            int last = network.Neurons.Count - 1;

            Network rewired = network.WithRule(last, 0, network.Neurons[last].Rules[0].WithDelay(2));
            Network refilled = network.WithNeuron(last, network.Neurons[last].WithInitialSpikes(5));
            Network hostChanged = network.WithRule(0, 0, network.Neurons[0].Rules[0].WithDelay(2));

            Assert.False(ModuleEdits.KeepsModules(network, rewired));
            Assert.True(ModuleEdits.KeepsModules(network, refilled));
            Assert.True(ModuleEdits.KeepsModules(network, hostChanged));
            Assert.Same(network, new ProtectModules(new DelegateMutation(_ => rewired)).Mutate(network, new Random(1)));
            Assert.All(new DissolveModule().Mutate(network, new Random(1)).Neurons, neuron => Assert.Null(neuron.Module));
        }

        private sealed class DelegateMutation : IMutation
        {
            private readonly Func<Network, Network> edit;

            public DelegateMutation(Func<Network, Network> edit) => this.edit = edit;

            public Network Mutate(Network network, Random random) => edit(network);
        }

        [Fact]
        public void AModuleThatTakesTheOutputIsFedByTheOldOne()
        {
            var library = new ModuleLibrary();
            Module module = ModuleOf(library, TriggeredTwos());
            int takeovers = 0;

            for (int seed = 0; seed < 20; seed++)
            {
                Network inserted = ModuleEdits.Insert(PingPong(), module, 1, 10, library, new Random(seed));
                int output = inserted.Neurons.ToList().FindIndex(neuron => neuron.IsOutput);
                if (output >= PingPong().Neurons.Count)
                {
                    takeovers++;
                    int oldOutput = PingPong().Neurons.ToList().FindIndex(neuron => neuron.IsOutput);
                    Assert.Contains(output + 1, inserted.Neurons[oldOutput].Connections);
                }
            }
            Assert.InRange(takeovers, 1, 19);
        }

        [Fact]
        public void ModuleTagsSurviveSavingAndAreShownInTheNotation()
        {
            var library = new ModuleLibrary();
            Network network = ModuleEdits.Insert(PingPong(), ModuleOf(library, Chain()), 3, 10, library, new Random(1));

            Network loaded = NetworkFiles.FromJson(NetworkFiles.ToJson(network))!;

            Assert.Equal(network.Neurons.Select(neuron => neuron.Module), loaded.Neurons.Select(neuron => neuron.Module));
            Assert.Contains("[module 1]", NetworkNotation.Format(network));
            Assert.DoesNotContain("Module", NetworkFiles.ToJson(PingPong()));
        }

    }
}
