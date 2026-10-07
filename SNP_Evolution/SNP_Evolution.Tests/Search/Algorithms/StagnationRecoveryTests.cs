using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Genome;
using SnpEvolution.Search.Operators;
using static SnpEvolution.Tests.Fixtures.GeneticAlgorithms;

namespace SnpEvolution.Tests.Search.Algorithms
{
    public class StagnationRecoveryTests
    {
        [Fact]
        public void StagnationEscalatesThenRestartsAndCalmsDownOnANewTask()
        {
            var random = new Random(2);
            NetworkFactory factory = Factories.StandardRules(random);
            var pressure = new MutationPressure();
            var evaluator = new RecordingEvaluator(_ => 0.5f);
            var recovery = new StagnationRecovery(
                Generational(8, random, factory.NewNetwork, evaluator, WeightedMutation.Structural(0.5f, factory, pressure)),
                new StagnationPolicy(Patience: 2, ImmigrantFraction: 0.25, MaxExtraEdits: 2), 8, pressure,
                factory.NewNetwork, WeightedMutation.Structural(1, factory), random, _ => { });

            recovery.NextGeneration();
            for (int generation = 0; generation < 4; generation++)
            {
                recovery.NextGeneration();
            }
            Assert.Equal((2, 0, 2), (recovery.Escalations, recovery.Restarts, pressure.ExtraEdits));

            recovery.NextGeneration();
            recovery.NextGeneration();
            Assert.Equal((1, 0), (recovery.Restarts, pressure.ExtraEdits));
            Assert.Equal(8, recovery.Population.Count);

            recovery.NextGeneration();
            recovery.NextGeneration();
            Assert.Equal(1, pressure.ExtraEdits);
            recovery.Rescore();
            Assert.Equal(0, pressure.ExtraEdits);
        }
    }
}
