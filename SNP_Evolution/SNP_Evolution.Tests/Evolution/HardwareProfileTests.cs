using SnpEvolution.Cli;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Evolution
{
    public class HardwareProfileTests
    {
        private static readonly GenomeSpace ProfileSpace = new GenomeSpace(InputCount: 2, RuleForm: RuleForm.Mixed, MaxNeurons: 8, MaxDelay: 3, DuplicateNeurons: true, HardwareProfile: true);

        // A fitness and checks that vary with size, so selection and lexicase have something to choose by.
        private static RecordingEvaluator Evaluator() => new RecordingEvaluator(network => 1f / (1 + network.Size % 7), network => new[] { network.Size % 2f });

        private static NetworkFactory Factory(int seed)
        {
            var random = new Random(seed);
            return new NetworkFactory(ProfileSpace, new ExpressionGenerator(ExpressionGenerator.ExperimentalTemplates, 4, random), random);
        }

        [Fact]
        public void NetworksMadeUnderTheProfileFitItWithoutBeingConformed()
        {
            NetworkFactory factory = Factory(1);

            for (int network = 0; network < 300; network++)
            {
                Network made = factory.NewNetwork();
                Assert.Empty(HardwareProfile.Problems(made));
            }
        }

        [Fact]
        public void NoNetworkMadeOrMutatedByAnyAlgorithmUnderTheProfileBreaksIt()
        {
            foreach (AlgorithmChoice choice in AlgorithmCatalog.All.Where(choice => !AlgorithmCatalog.IsComposition(choice.Name)))
            {
                NetworkFactory factory = Factory(choice.Name.Length);
                var evaluator = Evaluator();
                IGeneticAlgorithm algorithm = choice.Create(new EvolutionContext(20, 1f, factory.Random, factory.NewNetwork, evaluator, factory, _ => { }, Lexicase: true));

                for (int generation = 0; generation < 15; generation++)
                {
                    algorithm.NextGeneration();
                }

                Assert.True(evaluator.Seen.Count > 100, choice.Name);
                Network? broken = evaluator.Seen.FirstOrDefault(network => !HardwareProfile.Fits(network));
                Assert.True(broken == null, $"{choice.Name} made a network outside the profile: {string.Join(" ", broken == null ? new string[0] : HardwareProfile.Problems(broken))}");
            }
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
        public void PartSearchUnderTheProfileFindsAndShrinksAProfilePart()
        {
            var settings = new PartSearchSettings(5_000, 1_000, 30, Catalog.ChoiceFor(Catalog.StructuralDefault), () => new ExhaustiveCpuEngine(), HardwareProfile: true);

            PartOutcome outcome = PartEvolution.Evolve(ReferenceParts.DelayContract(2), 1, settings, _ => { });

            Assert.True(outcome.Solved);
            Assert.Empty(HardwareProfile.Problems(outcome.Part!.Network));
        }

        [Fact]
        public void ShrinkingUnderTheProfileNeverLeavesIt()
        {
            var random = new Random(4);
            var factory = new NetworkFactory(new GenomeSpace(InputCount: 1, MaxDelay: 3, HardwareProfile: true), new ExpressionGenerator(ExpressionGenerator.ExperimentalTemplates, 4, random), random);
            (var crossover, var edits) = PartEvolution.ShrinkOperators(factory, hardwareProfile: true);
            Network network = factory.NewNetwork();

            for (int step = 0; step < 300; step++)
            {
                network = edits.Mutate(crossover.Cross(network, factory.NewNetwork(), random), random);
                Assert.Empty(HardwareProfile.Problems(network));
            }
        }

        [Fact]
        public void RejectsAParityConditionAndNamesTheRule()
        {
            var network = new Network(new[]
            {
                Neuron(0, new[] { 2 }, HardwareProfile.ThresholdRule(2)),
                OutputNeuron(0, new Rule("a(aa)*", 0, true)),
            });

            string problem = Assert.Single(HardwareProfile.Problems(network));

            Assert.StartsWith("Neuron 2, rule 1 (a(aa)* -> a):", problem);
            Assert.Contains("accepts 1, 3, 5, 7, 9, ...", problem);
        }

        [Fact]
        public void NamesEveryOtherWayOutOfTheProfile()
        {
            var network = new Network(new[]
            {
                Neuron(2, new[] { 2 }, Standard("a+", 1, produce: 2), new Rule("aa+", 2, true)),
            });

            IReadOnlyList<string> problems = HardwareProfile.Problems(network);

            Assert.Contains(problems, problem => problem.Contains("starts with 2 spike(s)"));
            Assert.Contains(problems, problem => problem.Contains("has 2 rules"));
            Assert.Contains(problems, problem => problem.Contains("rule 1 (a+/a -> aa): it consumes 1 spike(s)"));
            Assert.Contains(problems, problem => problem.Contains("rule 1 (a+/a -> aa): it sends 2 spikes"));
            Assert.Contains(problems, problem => problem.Contains("rule 2 (aa+ -> a;2): its delay of 2 holds the neuron"));
        }

        [Theory]
        [InlineData("a+", 1)]
        [InlineData("aa+", 2)]
        [InlineData("a{3,}", 3)]
        [InlineData("aaa*", 2)]
        [InlineData("(aa|aaa)a*", 2)]
        [InlineData("a*", 1)]
        [InlineData("a(aa)*", null)]
        [InlineData("aaa", null)]
        [InlineData("b", null)]
        public void ReadsTheThresholdOfAThresholdCondition(string expression, int? threshold)
        {
            Assert.Equal(threshold, HardwareProfile.Threshold(SpikeCondition.Parse(expression)));
        }

        [Fact]
        public void ConformsToTheNearestThresholdRule()
        {
            Rule conformed = HardwareProfile.Conform(Standard("a(aa)+", 2, delay: 2));
            Assert.Equal(("a{3,}", 2, true, true, false), (conformed.Expression, conformed.Delay, conformed.Fire, conformed.Axonal, conformed.IsStandard));

            Rule forgetting = HardwareProfile.Conform(new Rule("aa", 1, false));
            Assert.Equal(("a{2,}", 0, false, false), (forgetting.Expression, forgetting.Delay, forgetting.Fire, forgetting.Axonal));

            Neuron neuron = HardwareProfile.Conform(Neuron(3, new[] { 2 }, new Rule("aa+", 0, true), new Rule("a", 0, true)));
            Assert.Equal(0, neuron.InitialSpikes);
            Assert.Single(neuron.Rules);
        }

        [Fact]
        public void LeavesAFittingNetworkAsItIs()
        {
            var network = new Network(new[] { Neuron(0, new[] { 2 }, HardwareProfile.ThresholdRule(1, delay: 2)), OutputNeuron(0, HardwareProfile.ThresholdRule(3, fire: false)) });

            Assert.Same(network, HardwareProfile.Conform(network));
        }

        [Fact]
        public void IsOffUnlessAskedFor()
        {
            Assert.False(new GenomeSpace().HardwareProfile);
            Assert.False(new PartSearchSettings(1, 1, 1, AlgorithmCatalog.All[0], () => null!).HardwareProfile);
        }
    }
}
