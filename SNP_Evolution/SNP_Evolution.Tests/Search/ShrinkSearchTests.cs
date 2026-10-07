using SnpEvolution.Compilation;
using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Genome;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using static SnpEvolution.Tests.Fixtures.Runs;

namespace SnpEvolution.Tests.Search
{
    public class ShrinkSearchTests
    {
        [Fact]
        [Slow]
        public void ShrinkingKeepsTheNetworkCorrectAndNeverGrowsIt()
        {
            int[] values = { 1, 2, 4, 8, 16, 32 };
            Network compiled = RecurrenceCompiler.Compile(Recurrence.Fit(values)!);
            FitnessEvaluator evaluator = SequenceEvaluator(values);
            var random = new Random(5);
            var space = new GenomeSpace(RuleForm: RuleForm.Standard, MaxNeurons: compiled.Neurons.Count, MaxInitialSpikes: 8, MaxProduce: 2);
            var factory = Factories.Networks(space, random, ExpressionGenerator.SimpleTemplates);
            var setup = new NetworkSetup(20, 0.5f, factory, () => compiled, new NetworkScoring(() => new SequentialCpuEngine(), new SimulationOptions(50, 2, OutputTiming.Interval), 1));

            SearchOutcome<Individual> shrink = SearchCatalog.Shrink.Run(new SearchRequest<Individual>(evaluator.Task, new EvaluationBudget(), random, _ => { })
            {
                Seeds = new[] { new Individual(compiled) },
                MaxGenerations = 30,
                Networks = setup,
            });

            Assert.Equal(30, shrink.Generations);
            Assert.True(HardwareCost.Of(shrink.Best!.Genes).CompareTo(HardwareCost.Of(compiled)) <= 0);
            Assert.True(evaluator.ConfirmSolved(shrink.Best!.Genes).Solved);
        }

        [Fact]
        public void ShrinkingUnderTheProfileNeverLeavesIt()
        {
            var random = new Random(4);
            var factory = Factories.Networks(new GenomeSpace(InputCount: 1, MaxDelay: 3, HardwareProfile: true), random);
            var (crossover, edits) = ShrinkSearch.Operators(factory);
            Network network = factory.NewNetwork();

            for (int step = 0; step < 300; step++)
            {
                network = edits.Mutate(crossover.Cross(network, factory.NewNetwork(), random), random);
                Assert.Empty(HardwareProfile.Problems(network));
            }
        }
    }
}
