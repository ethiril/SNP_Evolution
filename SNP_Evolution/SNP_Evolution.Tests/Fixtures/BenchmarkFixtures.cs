using SnpEvolution.Search.Benchmarking;

namespace SnpEvolution.Tests.Fixtures
{
    internal static class BenchmarkFixtures
    {
        // One seed per algorithm and task, so a benchmark test stays quick.
        public static readonly BenchmarkSettings OneSeed = BenchmarkSettings.Default with { Seeds = 1 };
    }
}
