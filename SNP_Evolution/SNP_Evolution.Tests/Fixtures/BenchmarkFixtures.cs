using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Specs.Parts;

namespace SnpEvolution.Tests.Fixtures
{
    internal static class BenchmarkFixtures
    {
        // One seed per algorithm and task, so a benchmark test stays quick.
        public static readonly BenchmarkSettings OneSeed = BenchmarkSettings.Default with { Seeds = 1 };

        public static BenchmarkSettings WithParts(ModuleLibrary library) => OneSeed with { Parts = library.Parts.Select(module => module.Part!).ToList() };
    }
}
