using SnpEvolution.Specs.Accounting;

namespace SnpEvolution.Tests.Specs.Accounting
{
    // An early stop reaches the run that began it, and what that run starts in parallel, but nothing outside it.
    public class EarlyStopTests
    {
        [Fact]
        public void NothingIsRequestedOutsideAScope()
        {
            Assert.False(EarlyStop.Requested);
        }

        [Fact]
        public void ARequestReachesWorkTheRunStartsInParallelAndEndsWithTheScope()
        {
            bool[] seen = new bool[8];
            using (EarlyStop stop = EarlyStop.Begin())
            {
                stop.Request();
                Parallel.For(0, seen.Length, index => seen[index] = EarlyStop.Requested);
            }

            Assert.All(seen, Assert.True);
            Assert.False(EarlyStop.Requested);
        }

        [Fact]
        public void ARequestDoesNotReachCodeOutsideTheRun()
        {
            using EarlyStop stop = EarlyStop.Begin();
            stop.Request();
            bool outside = true;

            // A thread started without the run's context stands for a test or command running beside it.
            var thread = new Thread(() => outside = EarlyStop.Requested);
            thread.UnsafeStart();
            thread.Join();

            Assert.False(outside);
        }
    }
}
