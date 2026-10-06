using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Networks;
using static SnpEvolution.Tests.Evolution.ModuleFixtures;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Evolution
{
    public class ModuleLibraryTests
    {
        private sealed class DelegateEvaluator : IPopulationEvaluator
        {
            public DelegateEvaluator(Func<Network, FitnessResult> result) => Result = result;

            public Func<Network, FitnessResult> Result { get; }

            public IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks) => networks.Select(Result).ToList();
        }

        [Fact]
        public void TheLibraryKeepsEachPartOnceAndDropsTheLeastHelpfulWhenFull()
        {
            var library = new ModuleLibrary(capacity: 2);
            Module first = ModuleOf(library, Chain());
            Module second = ModuleOf(library, PingPong());

            Assert.Same(first, ModuleOf(library, Chain()));
            library.Credit(first.Id, improved: false);
            library.Credit(second.Id, improved: true);
            ModuleOf(library, AlwaysOutputsOne());

            Assert.Equal(2, library.Modules.Count);
            Assert.Null(library.Find(first.Id));
            Assert.Equal((1, 1), (second.Uses, second.Wins));
        }

        [Fact]
        public void TheTrackerCreditsNewModulesAndKeepsChangesThatGetANewCheckRight()
        {
            var library = new ModuleLibrary();
            Module module = ModuleOf(library, Chain());
            var tracker = new ModuleTracker(library);
            var results = new Dictionary<Network, FitnessResult>();
            IPopulationEvaluator evaluator = tracker.Watch(new DelegateEvaluator(network => results[network]));
            Network parent = PingPong();
            Network child = ModuleEdits.Insert(parent, module, library.NextInstance(), 10, library, new Random(1));
            Network closer = parent.WithNeuron(1, parent.Neurons[1].WithInitialSpikes(3));
            Network worse = parent.WithNeuron(0, parent.Neurons[0].WithInitialSpikes(3));
            results[parent] = new FitnessResult(0.2f, Array.Empty<int>(), Checks: new[] { 1f, 0f });
            results[closer] = new FitnessResult(0.3f, Array.Empty<int>(), Checks: new[] { 1f, 0.5f });
            results[child] = new FitnessResult(0.5f, Array.Empty<int>(), Checks: new[] { 1f, 1f });
            results[worse] = new FitnessResult(0.1f, Array.Empty<int>(), Checks: new[] { 0f, 0f });
            foreach (Network offspring in new[] { closer, child, worse })
            {
                tracker.RecordChild(parent, offspring);
            }

            evaluator.EvaluateAll(new[] { parent });
            evaluator.EvaluateAll(new[] { closer, child, worse });

            Assert.Equal((1, 1), (module.Uses, module.Wins));
            Assert.Equal(2, library.Modules.Count);
            Assert.Contains("0.200 to 0.500", library.Modules[1].Origin);
        }
    }
}
