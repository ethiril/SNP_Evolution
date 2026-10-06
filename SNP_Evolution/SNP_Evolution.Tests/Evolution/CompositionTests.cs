using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.Evolution.ModuleFixtures;

namespace SnpEvolution.Tests.Evolution
{
    public class CompositionTests
    {
        private const int MaxGlue = 7;

        // Verified parts of every port kind the first parts use, one with its output role on a port.
        private static ModuleLibrary Library()
        {
            var library = new ModuleLibrary();
            Part increment = ReferenceParts.Increment();
            library.AddPart(Verified(increment with { Network = increment.Network.WithNeuron(2, increment.Network.Neurons[2].WithRoles(true, false)) }), "a test");
            library.AddPart(Verified(ReferenceParts.Register(FirstParts.Larger) with { Contract = FirstParts.Named("register") }), "a test");
            library.AddPart(Verified(ReferenceParts.Add()), "a test");
            library.AddPart(Verified(ReferenceParts.Delay(2)), "a test");
            return library;
        }

        private static NetworkFactory Factory(int inputCount, Random random) =>
            new NetworkFactory(new GenomeSpace(InputCount: inputCount, RuleForm: RuleForm.Standard, MaxNeurons: MaxGlue),
                new ExpressionGenerator(ExpressionGenerator.ExperimentalTemplates, 4, random), random);

        private static string Json(Network network) => NetworkFiles.ToJson(network);

        [Fact]
        public void RandomCompositionsSurviveFlatteningAndRecovery()
        {
            ModuleLibrary library = Library();
            for (int seed = 0; seed < 200; seed++)
            {
                var random = new Random(seed);
                Composition composition = Composition.Random(library, Factory(seed % 3, random), 1 + seed % 4, random);

                Network flat = composition.Flatten(library);

                Assert.Equal(composition, Composition.Recover(flat, library));
                Assert.Equal(Json(flat), Json(Composition.Recover(flat, library)!.Flatten(library)));
            }
        }

        [Fact]
        public void CompositionsStillRoundTripAfterAnyEditTheSearchMakes()
        {
            ModuleLibrary library = Library();
            for (int seed = 0; seed < 50; seed++)
            {
                var random = new Random(seed);
                var space = new CompositionSpace(library, Factory(seed % 2, random), new CompositionMix(), random);
                WeightedMutation mutation = space.Mutation(1);
                Network network = space.NewNetwork();
                for (int step = 0; step < 20; step++)
                {
                    network = mutation.Mutate(network, random);

                    Composition composition = Composition.Recover(network, library)!;
                    Assert.Equal(composition, Composition.Recover(composition.Flatten(library), library));
                    Assert.Equal(Json(network), Json(composition.Flatten(library)));
                    Assert.True(composition.ThroughPorts(library));
                    Assert.InRange(composition.Glue.Count, 1, MaxGlue);
                }
            }
        }

        [Fact]
        public void TheTwoIncrementChainIsACompositionThatFlattensToTheSameNetwork()
        {
            var library = new ModuleLibrary();
            Module increment = library.AddPart(Verified(ReferenceParts.Increment()), "a test");
            var first = new Network(increment.Part!.Part.Network.Neurons.Select(neuron => neuron.WithModule(new ModuleTag(increment.Id, 1))).ToList());
            Network chained = ModuleEdits.Insert(first, increment, 2, 24, library, new Random(1));
            PartCopy copy = PartWiring.Copies(chained, library).Single(each => each.Tag.Instance == 1);

            var composition = new Composition(
                new[] { new PartInstance(1, increment.Id, 0), new PartInstance(2, increment.Id, 0) },
                Array.Empty<GlueNeuron>(),
                new[] { new PortWire(1, "out", 2, "n"), new PortWire(1, "done", 2, "start") },
                Array.Empty<Link>(),
                new[] { new Endpoint(1, copy["start"]), new Endpoint(1, copy["n"]) },
                Array.Empty<Endpoint>());

            Assert.Equal(Json(chained), Json(composition.Flatten(library)));
            Assert.Equal(composition, Composition.Recover(chained, library));
        }

        [Fact]
        public void FlatteningTagsEveryPartNeuronWithItsInstance()
        {
            ModuleLibrary library = Library();
            Composition composition = Composition.Random(library, Factory(1, new Random(3)), 3, new Random(3));

            Network flat = composition.Flatten(library);

            Assert.Equal(composition.Glue.Count, flat.Neurons.Count(neuron => neuron.Module == null));
            Assert.Equal(
                composition.Parts.Select(part => new ModuleTag(part.Module, part.Instance)),
                flat.Neurons.Where(neuron => neuron.Module != null).Select(neuron => neuron.Module!).Distinct());
        }

        [Fact]
        public void ACopyWhoseInsideChangedIsNotRecovered()
        {
            ModuleLibrary library = Library();
            Network flat = Composition.Random(library, Factory(0, new Random(2)), 2, new Random(2)).Flatten(library);
            int partNeuron = flat.Neurons.ToList().FindIndex(neuron => neuron.Module != null);

            Assert.Null(Composition.Recover(flat.WithNeuron(partNeuron, flat.Neurons[partNeuron].WithInitialSpikes(flat.Neurons[partNeuron].InitialSpikes + 1)), library));
            Assert.Null(Composition.Recover(flat.WithNeuron(partNeuron, flat.Neurons[partNeuron].WithRules(new[] { Rule.Standard("aaa", 3) })), library));
        }

        [Fact]
        public void RemovingAPartDropsItsWiresAndKeepsTheRest()
        {
            ModuleLibrary library = Library();
            for (int seed = 0; seed < 20; seed++)
            {
                var random = new Random(seed);
                Network network = Composition.Random(library, Factory(0, random), 3, random).Flatten(library);
                Composition before = Composition.Recover(network, library)!;

                Composition after = Composition.Recover(new RemovePart(library).Mutate(network, random), library)!;

                Assert.True(after.Parts.Count >= before.Parts.Count - 1);
                Assert.All(after.Parts, part => Assert.Contains(part, before.Parts));
                Assert.All(after.Wires, wire => Assert.Contains(wire, before.Wires));
                Assert.Equal(before.Glue.Count, after.Glue.Count);
            }
        }

        // Every network a composition run keeps is exactly glue and library parts: no part neuron is ever changed.
        [Theory]
        [InlineData("MAP-Elites")]
        [InlineData("tournament")]
        public void ARunInCompositionModeNeverChangesAPartNeuron(string algorithm)
        {
            ModuleLibrary library = Library();
            var random = new Random(5);
            NetworkFactory factory = Factory(1, random);
            var task = FunctionTask.Of("n + 2", n => n + 2, new[] { 1, 2, 3 });
            var evaluator = new FitnessEvaluator(new SequentialCpuEngine(), task, new SimulationOptions(30, 2, OutputTiming.Interval), 1, random);
            IGeneticAlgorithm run = AlgorithmCatalog.All.Single(choice => AlgorithmCatalog.IsComposition(choice.Name) && choice.Name.Contains(algorithm))
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
