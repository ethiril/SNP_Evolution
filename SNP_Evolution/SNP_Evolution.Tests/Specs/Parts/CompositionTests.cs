using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.Fixtures.CompositionFixtures;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;

namespace SnpEvolution.Tests.Specs.Parts
{
    public class CompositionTests
    {
        [Fact]
        [Slow]
        public void RandomCompositionsSurviveFlatteningAndRecovery()
        {
            ModuleLibrary library = Library();
            for (int seed = 0; seed < 200; seed++)
            {
                var random = new Random(seed);
                Composition composition = RandomComposition.Of(library, GlueFactory(seed % 3, random), 1 + seed % 4, random);

                Network flat = composition.Flatten(library);

                Assert.InRange(composition.Glue.Count, 1, seed % 3 + 3);

                Assert.Equal(composition, Composition.Recover(flat, library));
                Assert.Equal(NetworkFiles.ToJson(flat), NetworkFiles.ToJson(Composition.Recover(flat, library)!.Flatten(library)));
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

            Assert.Equal(NetworkFiles.ToJson(chained), NetworkFiles.ToJson(composition.Flatten(library)));
            Assert.Equal(composition, Composition.Recover(chained, library));
        }

        [Fact]
        public void FlatteningTagsEveryPartNeuronWithItsInstance()
        {
            ModuleLibrary library = Library();
            Composition composition = RandomComposition.Of(library, GlueFactory(1, new Random(3)), 3, new Random(3));

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
            Network flat = RandomComposition.Of(library, GlueFactory(0, new Random(2)), 2, new Random(2)).Flatten(library);
            int partNeuron = flat.Neurons.ToList().FindIndex(neuron => neuron.Module != null);

            Assert.Null(Composition.Recover(flat.WithNeuron(partNeuron, flat.Neurons[partNeuron].WithInitialSpikes(flat.Neurons[partNeuron].InitialSpikes + 1)), library));
            Assert.Null(Composition.Recover(flat.WithNeuron(partNeuron, flat.Neurons[partNeuron].WithRules(new[] { Rule.Standard("aaa", 3) })), library));
        }

        [Fact]
        public void ALinkIntoAPartsOutPortDoesNotPassThroughItsPorts()
        {
            ModuleLibrary library = Library();
            Composition composition = RandomComposition.Of(library, GlueFactory(0, new Random(4)), 1, new Random(4));
            PartInstance part = composition.Parts[0];
            PartPort outPort = Composition.PartOf(part, library).Part.Ports().First(port => port.Port.Direction == PortDirection.Out);
            Composition bypassing = composition with { Links = composition.Links.Append(new Link(Endpoint.GlueAt(1), new Endpoint(part.Instance, outPort.Position))).ToList() };

            Assert.True(composition.ThroughPorts(library));
            Assert.False(bypassing.ThroughPorts(library));
            Assert.Equal(composition, bypassing.OnlyThroughPorts(library));
        }

    }
}
