using SnpEvolution.Search;
using SnpEvolution.Search.Benchmarking;
using static SnpEvolution.Tests.Fixtures.BenchmarkFixtures;

namespace SnpEvolution.Tests.Search.Benchmarking
{
    public class AlgorithmSelectorTests
    {
        [Fact]
        [Slow]
        public void SelectorHalvesTheCandidatesUntilOneIsLeft()
        {
            List<EvolutionSearch> candidates = SearchCatalog.Evolution.Take(3).ToList();

            SelectionResult<EvolutionSearch> result = AlgorithmSelector.Select(candidates, TaskSuite.Generators[0], OneSeed, initialBudget: 60);

            Assert.Contains(result.Winner, candidates);
            Assert.Equal(new[] { 3, 2 }, result.Rounds.Select(round => round.Standings.Count));
            Assert.Equal(new[] { 60L, 120L }, result.Rounds.Select(round => round.Budget));
            Assert.Equal(result.Rounds[0].Standings.Take(2).Select(row => row.Algorithm), result.Rounds[0].Advancing);
            Assert.NotNull(result.BestFound);
        }
    }
}
