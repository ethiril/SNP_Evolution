using SnpEvolution.Model;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Modules;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Modules
{
    public class ModularEvolutionTests
    {
        // Stalls after two generations and hands a side run five more.
        private static readonly ModulePolicy QuickStall = new ModulePolicy(Patience: 2, SideGenerations: 5, CompositeFraction: 0.5, IncubationGenerations: 0);

        private static readonly SequenceTask Target = new SequenceTask("test", new[] { 2, 2, 5, 2 });

        // A main run whose best networks never improve, so it stalls.
        private static StubAlgorithm StalledMain()
        {
            var main = new StubAlgorithm();
            main.Individuals.AddRange(new[] { Scored(PingPong(), 0.5f, 1, 1, 0, 0), Scored(Chain(), 0.4f, 1, 0, 0, 0) });
            return main;
        }

        private static StubAlgorithm SolvedSideRun()
        {
            var side = new StubAlgorithm();
            side.Individuals.Add(Scored(AlwaysOutputsOne(), 1));
            return side;
        }

        // Each side run records the task it was given and solves it at once.
        private static Func<ITask, IReadOnlyList<Network>?, IGeneticAlgorithm> RecordingSideRuns(List<ITask> tasks) => (task, _) =>
        {
            tasks.Add(task);
            return SolvedSideRun();
        };

        private static ModularEvolution Modular(StubAlgorithm main, ModuleLibrary library, Func<ITask> task, Func<ITask, IReadOnlyList<Network>?, IGeneticAlgorithm> sideRuns, ModulePolicy policy) =>
            new ModularEvolution(main, library, null, task, sideRuns, policy, populationSize: 4, maxNeurons: 10, new Random(1), _ => { });

        [Fact]
        public void AStallBuildsAModuleOnTheSideAndOffersItToTheBestNetworks()
        {
            var library = new ModuleLibrary();
            StubAlgorithm main = StalledMain();
            var focusTasks = new List<ITask>();
            ModularEvolution modular = Modular(main, library, () => Target, RecordingSideRuns(focusTasks), QuickStall);

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
            var parts = new List<ITask>();
            ModularEvolution modular = Modular(StalledMain(), new ModuleLibrary(), () => Target, RecordingSideRuns(parts), QuickStall);

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
            StubAlgorithm main = StalledMain();
            var parts = new List<ITask>();
            var incubated = new List<IReadOnlyList<Network>>();
            ModularEvolution modular = Modular(main, library, () => Target, (sideTask, seeds) =>
            {
                if (seeds == null)
                {
                    parts.Add(sideTask);
                    return SolvedSideRun();
                }
                incubated.Add(seeds);
                var side = new StubAlgorithm();
                side.Individuals.AddRange(seeds.Select(seed => Scored(seed, 0.6f)));
                return side;
            }, QuickStall with { Triggered = false, IncubationGenerations = 4 });

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
            ModularEvolution modular = Modular(main, library, () => new SequenceTask("twos", new[] { 2, 2 }), (_, _) => new StubAlgorithm(), new ModulePolicy());

            modular.Rescore();

            Assert.Equal(1, main.Rescores);
            Assert.Contains("solved twos", library.Modules.Single().Origin);
        }
    }
}
