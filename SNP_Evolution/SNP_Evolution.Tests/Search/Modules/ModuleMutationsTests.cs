using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Search.Genome;
using SnpEvolution.Search.Modules;
using SnpEvolution.Search.Operators;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;
using static SnpEvolution.Tests.Fixtures.PartWiringFixtures;
using static SnpEvolution.Tests.Fixtures.WellFormedNetworks;

namespace SnpEvolution.Tests.Search.Modules
{
    public class ModuleMutationsTests
    {
        // The task's input feeds the part's in-port and its out-port feeds the task's, rather than any glue neuron at random.
        [Fact]
        public void AnInsertedPartIsWiredToTheTasksOwnPorts()
        {
            var library = new ModuleLibrary();
            library.AddPart(PartFixtures.Verified(ReferenceParts.Register()), "a test");
            var task = new ContractTask(FirstParts.Named("register"));
            var glue = new Network(Enumerable.Range(1, 4).Select(position => new Neuron(new[] { Rule.Standard("a", 1) }, 0, Array.Empty<int>(), false, isInput: position <= 2)).ToList());
            var insert = new InsertModule(library, new GenomeSpace(InputCount: 2, RuleForm: RuleForm.Standard, MaxNeurons: MaxNeurons), task.Boundary);

            for (int seed = 0; seed < 10; seed++)
            {
                Network network = insert.Mutate(glue, new Random(seed));
                PartCopy copy = PartWiring.Copies(network, library).Single();

                Assert.Contains(copy["n"], network.Neurons[1].Connections);
                Assert.Contains(task.Binding["out"], network.Neurons[copy["out"] - 1].Connections);
            }
        }

        [Fact]
        public void EditsWithModulesKeepNetworksWellFormedAndModulesWhole()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var random = new Random(seed);
                var factory = Factories.Networks(new GenomeSpace(InputCount: seed % 2, RuleForm: RuleForm.Standard, MaxNeurons: 12), random);
                var library = new ModuleLibrary();
                library.Add(ModuleCuts.Whole(Chain()), "a test");
                library.Add(ModuleCuts.Cut(Chain(), new[] { 1 }), "a test");
                Part increment = seed % 2 == 0 ? ReferenceParts.Increment() : PartFixtures.PaddedIncrement();
                library.AddPart(PartFixtures.Measured(increment), "a test");
                WeightedMutation mutation = WeightedMutation.Structural(1, factory, modules: new ModuleSupport(library));
                Network network = factory.NewNetwork();
                for (int step = 0; step < 150; step++)
                {
                    WeightedEdit edit = mutation.Edits[random.Next(mutation.Edits.Count)];
                    Network next = edit.Edit.Mutate(network, random);
                    Assert.True(edit.Edit is DissolveModule or SwapPart || ModuleEdits.KeepsModules(network, next), edit.Name);
                    network = next;
                    AssertWellFormed(network, factory.Space);
                }
            }
        }

        [Fact]
        public void ProtectingModulesRefusesEditsToTheirInsidesAndDissolvingUntagsThem()
        {
            var library = new ModuleLibrary();
            Module module = ModuleOf(library, Chain());
            Network network = ModuleEdits.Insert(TestNetworks.PingPong(), module, 1, 10, library, new Random(1));
            int last = network.Neurons.Count - 1;
            Network rewired = network.WithRule(last, 0, network.Neurons[last].Rules[0].WithDelay(2));

            Assert.Same(network, new ProtectModules(new DelegateMutation(_ => rewired)).Mutate(network, new Random(1)));
            Assert.All(new DissolveModule().Mutate(network, new Random(1)).Neurons, neuron => Assert.Null(neuron.Module));
        }

        private sealed class DelegateMutation : IMutation
        {
            private readonly Func<Network, Network> edit;

            public DelegateMutation(Func<Network, Network> edit) => this.edit = edit;

            public Network Mutate(Network network, Random random) => edit(network);
        }
    }
}
