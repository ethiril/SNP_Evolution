using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Algorithms;

namespace SnpEvolution.Tests.Evolution
{
    public class StallDetectorTests
    {
        [Fact]
        public void StallsAfterPatienceGenerationsWithoutImprovementAndStartsCountingAgain()
        {
            var stall = new StallDetector(patience: 3);

            Assert.Equal(Progress.Improved, stall.Observe(0.5f));
            Assert.Equal(Progress.Waiting, stall.Observe(0.5f));
            Assert.Equal(Progress.Waiting, stall.Observe(0.4f));
            Assert.Equal(Progress.Stalled, stall.Observe(0.5f));
            Assert.Equal(Progress.Waiting, stall.Observe(0.5f));
            Assert.Equal(Progress.Improved, stall.Observe(0.6f));
            Assert.Equal(0.6f, stall.Best);
        }

        // Sampled scores of the same network vary by less than this, so it is no improvement.
        [Fact]
        public void ATinyGainIsNotAnImprovement()
        {
            var stall = new StallDetector(patience: 1);
            stall.Observe(0.5f);

            Assert.Equal(Progress.Stalled, stall.Observe(0.5f + 1e-7f));
        }

        [Fact]
        public void AResetForgetsTheBestAndTheCount()
        {
            var stall = new StallDetector(patience: 2);
            stall.Observe(0.9f);
            stall.Wait();
            stall.Reset();

            Assert.Null(stall.Best);
            Assert.Equal(Progress.Improved, stall.Observe(0.1f));
            Assert.Equal(Progress.Waiting, stall.Wait());
        }

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
    }
}
