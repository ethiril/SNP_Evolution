using SnpEvolution.Model;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Modules;
using SnpEvolution.Specs.Parts;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Modules
{
    public class ModuleTrackerTests
    {
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
