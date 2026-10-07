using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Genome;

namespace SnpEvolution.Tests.Search
{
    public class EvolutionContextTests
    {
        private static readonly GenomeSpace ProfileSpace = new GenomeSpace(InputCount: 2, RuleForm: RuleForm.Mixed, MaxNeurons: 8, MaxDelay: 3, DuplicateNeurons: true, HardwareProfile: true);

        // A fitness and checks that vary with size, so selection and lexicase have something to choose by.
        private static RecordingEvaluator Evaluator() => new RecordingEvaluator(network => 1f / (1 + network.Size % 7), network => new[] { network.Size % 2f });

        private static NetworkFactory Factory(int seed)
        {
            var random = new Random(seed);
            return Factories.Networks(ProfileSpace, random);
        }

        public static TheoryData<string> ProfileAlgorithms => new TheoryData<string>(
            SearchCatalog.Evolution.Where(search => search is not CompositionSearch).Select(search => search.Name));

        [Theory]
        [MemberData(nameof(ProfileAlgorithms))]
        public void NoNetworkMadeOrMutatedByAnyAlgorithmUnderTheProfileBreaksIt(string name)
        {
            EvolutionSearch choice = SearchCatalog.Evolution.Single(search => search.Name == name);
            NetworkFactory factory = Factory(choice.Name.Length);
            var evaluator = Evaluator();
            IGeneticAlgorithm algorithm = choice.Create(new EvolutionContext(20, 1f, factory.Random, factory.NewNetwork, evaluator, factory, _ => { }, Lexicase: true));

            for (int generation = 0; generation < 15; generation++)
            {
                algorithm.NextGeneration();
            }

            Assert.True(evaluator.Seen.Count > 100, choice.Name);
            Assert.All(evaluator.Seen, network =>
                Assert.True(HardwareProfile.Fits(network), $"{choice.Name} made a network outside the profile: {string.Join(" ", HardwareProfile.Problems(network))}"));
        }

        [Fact]
        public void EveryStructuralEditIsPutBackWithinTheProfile()
        {
            NetworkFactory factory = Factory(7);
            var context = new EvolutionContext(10, 1f, factory.Random, factory.NewNetwork, Evaluator(), factory, _ => { });
            foreach (var edit in context.StructuralMutation(1).Edits)
            {
                var conformed = context.Conformed(edit.Edit);
                Network network = factory.NewNetwork();
                for (int step = 0; step < 200; step++)
                {
                    network = conformed.Mutate(network, factory.Random);
                    Assert.True(HardwareProfile.Fits(network), $"{edit.Name}: {string.Join(" ", HardwareProfile.Problems(network))}");
                }
            }
        }

        [Fact]
        public void EveryStructuralEditIsPutBackWithinADeterministicSpace()
        {
            NetworkFactory factory = Factories.Networks(new GenomeSpace(InputCount: 1, RuleForm: RuleForm.Standard, MaxNeurons: 8, MaxDelay: 3, Deterministic: true), new Random(7));
            var context = new EvolutionContext(10, 1f, factory.Random, factory.NewNetwork, Evaluator(), factory, _ => { });
            foreach (var edit in context.StructuralMutation(1).Edits)
            {
                var conformed = context.Conformed(edit.Edit);
                Network network = factory.NewNetwork();
                for (int step = 0; step < 200; step++)
                {
                    network = conformed.Mutate(network, factory.Random);
                    Assert.True(DeterministicNeurons.Fits(network), edit.Name);
                }
            }
        }
    }
}
