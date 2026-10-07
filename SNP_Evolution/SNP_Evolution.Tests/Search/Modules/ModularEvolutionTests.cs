using SnpEvolution.Model;
using SnpEvolution.Search.Modules;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Modules
{
    public class ModularEvolutionTests
    {
        [Fact]
        public void AStallBuildsAModuleOnTheSideAndOffersItToTheBestNetworks()
        {
            var library = new ModuleLibrary();
            var task = new SequenceTask("test", new[] { 2, 2, 5, 2 });
            var main = new StubAlgorithm();
            main.Individuals.AddRange(new[] { Scored(PingPong(), 0.5f, 1, 1, 0, 0), Scored(Chain(), 0.4f, 1, 0, 0, 0) });
            var focusTasks = new List<ITask>();
            var modular = new ModularEvolution(main, library, null, () => task, (focus, _) =>
            {
                focusTasks.Add(focus);
                var side = new StubAlgorithm();
                side.Individuals.Add(Scored(AlwaysOutputsOne(), 1));
                return side;
            }, new ModulePolicy(Patience: 2, SideGenerations: 5, CompositeFraction: 0.5, IncubationGenerations: 0), 4, 10, new Random(1), _ => { });

            for (int generation = 0; generation < 3; generation++)
            {
                modular.NextGeneration();
            }

            Assert.Equal(1, modular.SideRuns);
            Assert.Equal(new[] { 2, 5, 2 }, ((SequenceTask)focusTasks.Single()).Expected);
            Assert.Single(library.Modules);
            Assert.Equal(2, main.Immigrants.Count);
            Assert.All(main.Immigrants, network => Assert.Contains(network.Neurons, neuron => neuron.Module?.Module == library.Modules[0].Id));
        }

        [Fact]
        public void EveryOtherSideRunBuildsAPartThatWaitsForATrigger()
        {
            var task = new SequenceTask("test", new[] { 2, 2, 5, 2 });
            var main = new StubAlgorithm();
            main.Individuals.AddRange(new[] { Scored(PingPong(), 0.5f, 1, 1, 0, 0), Scored(Chain(), 0.4f, 1, 0, 0, 0) });
            var parts = new List<ITask>();
            var modular = new ModularEvolution(main, new ModuleLibrary(), null, () => task, (part, _) =>
            {
                parts.Add(part);
                var side = new StubAlgorithm();
                side.Individuals.Add(Scored(AlwaysOutputsOne(), 1));
                return side;
            }, new ModulePolicy(Patience: 2, SideGenerations: 5, CompositeFraction: 0.5, IncubationGenerations: 0), 4, 10, new Random(1), _ => { });

            for (int generation = 0; generation < 5; generation++)
            {
                modular.NextGeneration();
            }

            Assert.Equal(2, parts.Count);
            Assert.IsType<SequenceTask>(parts[0]);
            Assert.IsType<TriggeredSequenceTask>(parts[1]);
        }

        [Fact]
        public void NetworksGivenAModuleIncubateAndASolvedPartIsNotEvolvedAgain()
        {
            var library = new ModuleLibrary();
            var task = new SequenceTask("test", new[] { 2, 2, 5, 2 });
            var main = new StubAlgorithm();
            main.Individuals.AddRange(new[] { Scored(PingPong(), 0.5f, 1, 1, 0, 0), Scored(Chain(), 0.4f, 1, 0, 0, 0) });
            var parts = new List<ITask>();
            var incubated = new List<IReadOnlyList<Network>>();
            var modular = new ModularEvolution(main, library, null, () => task, (sideTask, seeds) =>
            {
                var side = new StubAlgorithm();
                if (seeds == null)
                {
                    parts.Add(sideTask);
                    side.Individuals.Add(Scored(AlwaysOutputsOne(), 1));
                }
                else
                {
                    incubated.Add(seeds);
                    side.Individuals.AddRange(seeds.Select(seed => Scored(seed, 0.6f)));
                }
                return side;
            }, new ModulePolicy(Patience: 2, SideGenerations: 5, CompositeFraction: 0.5, Triggered: false, IncubationGenerations: 4), 4, 10, new Random(1), _ => { });

            for (int generation = 0; generation < 5; generation++)
            {
                modular.NextGeneration();
            }

            Module module = library.Modules.Single();
            Assert.Single(parts);
            Assert.Equal(2, incubated.Count);
            Assert.All(incubated, seeds => Assert.All(seeds, network => Assert.Contains(network.Neurons, neuron => neuron.Module?.Module == module.Id)));
            Assert.Equal(incubated.SelectMany(seeds => seeds), main.Immigrants);
            Assert.Equal((2, 2), (module.Uses, module.Wins));
        }

        [Fact]
        public void ASolvedStageIsKeptAsAModule()
        {
            var library = new ModuleLibrary();
            var main = new StubAlgorithm();
            main.Individuals.Add(Scored(PingPong(), 1));
            var modular = new ModularEvolution(main, library, null, () => new SequenceTask("twos", new[] { 2, 2 }), (_, _) => new StubAlgorithm(),
                new ModulePolicy(), 4, 10, new Random(1), _ => { });

            modular.Rescore();

            Assert.Equal(1, main.Rescores);
            Assert.Contains("solved twos", library.Modules.Single().Origin);
        }
    }
}
