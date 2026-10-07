using SnpEvolution.Search.Algorithms;
using SnpEvolution.Specs.Accounting;

namespace SnpEvolution.Tests.Search.Algorithms
{
    public class GenerationLoopTests
    {
        [Fact]
        public void TheLoopLooksAtTheBudgetBeforeEachGenerationAndFinishesOneStarted()
        {
            int run = 0;

            (SearchStop stop, int generations) = GenerationLoop.Run(10, () => run >= 3, default, _ => ++run > 100);

            Assert.Equal(SearchStop.BudgetSpent, stop);
            Assert.Equal(3, generations);
        }

        [Fact]
        public void TheLoopStopsOnTheGenerationThatSolves()
        {
            (SearchStop stop, int generations) = GenerationLoop.Run(10, () => false, default, generation => generation == 4);

            Assert.Equal(SearchStop.Solved, stop);
            Assert.Equal(5, generations);
        }

        [Fact]
        public void ACancelledLoopRunsNoMoreGenerations()
        {
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();

            Assert.Equal((SearchStop.Cancelled, 0), GenerationLoop.Run(10, () => false, cancel.Token, _ => false));
        }

        [Fact]
        public void AnEarlyStopEndsTheLoopAfterTheGenerationItCameIn()
        {
            using EarlyStop stop = EarlyStop.Begin();

            (SearchStop result, int generations) = GenerationLoop.Run(10, () => false, default, generation =>
            {
                if (generation == 2)
                {
                    stop.Request();
                }
                return false;
            });

            Assert.Equal((SearchStop.Cancelled, 3), (result, generations));
        }
    }
}
