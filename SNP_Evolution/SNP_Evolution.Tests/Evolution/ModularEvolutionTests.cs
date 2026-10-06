using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.TestNetworks;
using static SnpEvolution.Tests.Evolution.ModuleFixtures;

namespace SnpEvolution.Tests.Evolution
{
    public class ModularEvolutionTests
    {
        // A fixed population that records what it is given, so the modular loop can be watched without evolving.
        private sealed class StubAlgorithm : IGeneticAlgorithm
        {
            public List<Individual> Individuals { get; } = new List<Individual>();

            public List<Network> Immigrants { get; } = new List<Network>();

            public int Rescores { get; private set; }

            public IReadOnlyList<Individual> Population => Individuals;

            public int Generation { get; private set; } = 1;

            public Individual? Best => Individuals.Count == 0 ? null : Ranking.Rank(Individuals)[0];

            public IReadOnlyList<IReadOnlyList<float>> FitnessHistory => Array.Empty<IReadOnlyList<float>>();

            public void NextGeneration() => Generation++;

            public void Immigrate(IReadOnlyList<Network> newcomers) => Immigrants.AddRange(newcomers);

            public void Rescore() => Rescores++;
        }

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
