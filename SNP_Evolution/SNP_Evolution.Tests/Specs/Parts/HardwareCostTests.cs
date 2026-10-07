using SnpEvolution.Model;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Genome;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Tests.Specs.Parts
{
    public class HardwareCostTests
    {
        private static HardwareCost Measure(ContractTask task, Network network)
        {
            var trials = task.Cases.Select(@case => new Trial(network, @case.Input, @case.Readout, @case.Watch)).ToList();
            IReadOnlyList<TrialResult> results = new ExhaustiveCpuEngine().Run(trials, new SimulationOptions(task.StepsNeeded, 5, OutputTiming.Interval), new Random(0));
            return HardwareCost.Of(network, results);
        }

        // Two neurons, start -> done; "a" takes three lasso entries (0, 1, then never again) and "a+" two (0, then 1 forever).
        [Fact]
        public void TheReferenceDelayCostsTwoNeuronsOneSynapseAndOneSpike()
        {
            Part delay = ReferenceParts.Delay(3);

            Assert.Equal(new HardwareCost(Neurons: 2, Synapses: 1, DistinctRules: 2, Rules: 2, RegisterWidth: 1, LassoTable: 5), Measure(delay.Task(), delay.Network));
        }

        // The store holds 2n from the count port and one more from start, so 25 spikes for n = 12. Five "a" rules take three
        // lasso entries each, two "aa" four each, and "a(aa)+" four.
        [Fact]
        public void TheReferenceRegisterNeedsAStoreTwiceAsWideAsItsLargestCount()
        {
            Part register = ReferenceParts.Register();

            HardwareCost cost = Measure(new ContractTask(FirstParts.Named("register"), register.Binding), register.Network);

            Assert.Equal(new HardwareCost(Neurons: 5, Synapses: 4, DistinctRules: 6, Rules: 8, RegisterWidth: 2 * FirstParts.Larger + 1, LassoTable: 27), cost);
        }

        [Fact]
        public void WithoutRunsTheRegisterWidthIsTheLargestStartingCount()
        {
            Network network = new Network(new[]
            {
                new Neuron(new[] { Rule.Standard("a", 1) }, 3, new[] { 2 }, false),
                new Neuron(new[] { Rule.Standard("a", 1) }, 7, new int[0], false),
            });

            Assert.Equal(7, HardwareCost.Of(network).RegisterWidth);
        }

        public static TheoryData<HardwareCost, HardwareCost> SmallerFirst => new TheoryData<HardwareCost, HardwareCost>
        {
            // Fewer neurons wins over everything else.
            { new HardwareCost(2, 9, 9, 9, 99, 99), new HardwareCost(3, 1, 1, 1, 1, 1) },
            // Then fewer synapses.
            { new HardwareCost(3, 1, 9, 9, 99, 99), new HardwareCost(3, 2, 1, 1, 1, 1) },
            // Then fewer rules, ahead of distinct rules.
            { new HardwareCost(3, 2, 9, 4, 99, 99), new HardwareCost(3, 2, 1, 5, 1, 1) },
            // Then a narrower register.
            { new HardwareCost(3, 2, 9, 4, 5, 99), new HardwareCost(3, 2, 1, 4, 6, 1) },
            // Then fewer distinct rules, then a smaller lasso table.
            { new HardwareCost(3, 2, 1, 4, 5, 99), new HardwareCost(3, 2, 2, 4, 5, 1) },
            { new HardwareCost(3, 2, 1, 4, 5, 6), new HardwareCost(3, 2, 1, 4, 5, 7) },
        };

        [Theory]
        [MemberData(nameof(SmallerFirst))]
        public void CostsOrderByNeuronsSynapsesRulesThenRegisterWidth(HardwareCost smaller, HardwareCost larger)
        {
            Assert.True(smaller.CompareTo(larger) < 0);
            Assert.True(larger.CompareTo(smaller) > 0);
            Assert.Equal(new[] { smaller, larger }, new[] { larger, smaller }.OrderBy(cost => cost, HardwareCost.SmallestFirst));
        }

        [Fact]
        public void HardwareCostCanBeAMapElitesCell()
        {
            var random = new Random(1);
            var factory = Factories.Networks(new GenomeSpace(MaxNeurons: 5), random, ExpressionGenerator.SimpleTemplates, length: 3);
            var elites = new MapElites(10, random, factory.NewNetwork, new ConstantEvaluator(), new SnpEvolution.Search.Operators.NeuronCrossover(),
                SnpEvolution.Search.WeightedMutation.Structural(1, factory), cells: HardwareCost.Cell);

            elites.NextGeneration();
            elites.NextGeneration();

            var cells = elites.Population.Select(individual => HardwareCost.Cell(individual.Genes)).ToList();
            Assert.Equal(cells.Count, cells.Distinct().Count());
        }

        private sealed class ConstantEvaluator : IPopulationEvaluator
        {
            public IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks) =>
                networks.Select(_ => new FitnessResult(0.5f, Array.Empty<int>())).ToList();
        }
    }
}
