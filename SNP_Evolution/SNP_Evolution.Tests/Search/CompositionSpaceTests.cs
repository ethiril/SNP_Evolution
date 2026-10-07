using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Genome;
using SnpEvolution.Search.Modules;
using SnpEvolution.Search.Operators;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.Fixtures.CompositionFixtures;

namespace SnpEvolution.Tests.Search
{
    public class CompositionSpaceTests
    {
        private sealed class FixedEdit : IMutation
        {
            private readonly Network result;

            public FixedEdit(Network result) => this.result = result;

            public Network Mutate(Network network, Random random) => result;
        }

        [Fact]
        [Slow]
        public void CompositionsStillRoundTripAfterAnyEditTheSearchMakes()
        {
            ModuleLibrary library = Library();
            for (int seed = 0; seed < 50; seed++)
            {
                var random = new Random(seed);
                var space = new CompositionSpace(library, GlueFactory(seed % 2, random), new CompositionMix(), random);
                WeightedMutation mutation = space.Mutation(1);
                Network network = space.NewNetwork();
                for (int step = 0; step < 20; step++)
                {
                    network = mutation.Mutate(network, random);

                    Composition composition = Composition.Recover(network, library)!;
                    Assert.Equal(composition, Composition.Recover(composition.Flatten(library), library));
                    Assert.Equal(NetworkFiles.ToJson(network), NetworkFiles.ToJson(composition.Flatten(library)));
                    Assert.True(composition.ThroughPorts(library));
                    Assert.InRange(composition.Glue.Count, 1, MaxGlue);
                }
            }
        }

        [Fact]
        public void TheSearchAdmitsNoMoreGlueThanItsCap()
        {
            ModuleLibrary library = Library();
            var random = new Random(6);
            Composition composition = RandomComposition.Of(library, GlueFactory(0, random), 1, random);
            var space = new CompositionSpace(library, GlueFactory(0, random), new CompositionMix(MaxGlue: composition.Glue.Count), random);
            Composition overGlued = composition with { Glue = composition.Glue.Append(composition.Glue[0]).ToList() };

            Assert.NotNull(space.Admit(composition.Flatten(library), dropStrayLinks: false));
            Assert.Null(space.Admit(overGlued.Flatten(library), dropStrayLinks: false));
        }

        // Glue may stop reading a part's port, but a glue edit that starts reading one would change how the part is used.
        [Fact]
        public void AGlueEditMayNotAddASynapseFromAPartToGlue()
        {
            ModuleLibrary library = Library();
            var random = new Random(8);
            var space = new CompositionSpace(library, GlueFactory(0, random), new CompositionMix(), random);
            Composition composition = RandomComposition.Of(library, GlueFactory(0, random), 1, random);
            PartInstance part = composition.Parts[0];
            PartPort outPort = Composition.PartOf(part, library).Part.Ports().First(port => port.Port.Direction == PortDirection.Out);
            var sender = new Endpoint(part.Instance, outPort.Position);
            int unread = Enumerable.Range(1, composition.Glue.Count).First(glue => !composition.Links.Contains(new Link(sender, Endpoint.GlueAt(glue))));
            Network network = composition.Flatten(library);
            Network reading = (composition with { Links = composition.Links.Append(new Link(sender, Endpoint.GlueAt(unread))).ToList() }).Flatten(library);

            Assert.Same(network, new CompositionEdit(space, new FixedEdit(reading), glueOnly: true).Mutate(network, random));
        }

        // Every network a composition run keeps is exactly glue and library parts: no part neuron is ever changed.
        [Theory]
        [Slow]
        [InlineData(true)]
        [InlineData(false)]
        public void ARunInCompositionModeNeverChangesAPartNeuron(bool mapElites)
        {
            ModuleLibrary library = Library();
            var random = new Random(5);
            NetworkFactory factory = GlueFactory(1, random);
            var task = FunctionTask.Of("n + 2", n => n + 2, new[] { 1, 2, 3 });
            var evaluator = Runs.SamplingEvaluator(task, random, maxSteps: 30);
            IGeneticAlgorithm run = (mapElites ? SearchCatalog.CompositionMapElites : SearchCatalog.CompositionTournament)
                .Create(new EvolutionContext(16, 0.5f, random, factory.NewNetwork, evaluator, factory, _ => { }, Parts: library));

            bool sawParts = false;
            for (int generation = 0; generation < 30; generation++)
            {
                run.NextGeneration();
                sawParts |= run.Population.Any(individual => individual.Genes.Neurons.Any(neuron => neuron.Module != null));
                Assert.All(run.Population, individual =>
                {
                    Composition? composition = Composition.Recover(individual.Genes, library);
                    Assert.NotNull(composition);
                    Assert.True(composition!.ThroughPorts(library));
                    Assert.InRange(composition.Glue.Count, 1, MaxGlue);
                });
            }
            Assert.True(sawParts);
        }
    }
}
