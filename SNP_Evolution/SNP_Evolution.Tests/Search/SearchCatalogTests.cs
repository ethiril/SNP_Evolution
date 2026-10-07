using SnpEvolution.Search;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Tests.Search
{
    // The benchmark command writes to the shared console.
    [Collection(ProcessStateCollection.Name)]
    public class SearchCatalogTests
    {
        private sealed class RandomSampling : ISearch<Individual>
        {
            public const string SearchName = "Random sampling, registered by a test";

            public string Name => SearchName;

            public SearchOutcome<Individual> Run(SearchRequest<Individual> request)
            {
                NetworkSetup setup = request.RequireNetworks();
                FitnessEvaluator evaluator = setup.Scoring.Evaluator(request.Task, request.Budget, request.Random);
                Individual? best = null;
                while (!request.Budget.IsSpent)
                {
                    var sample = new Individual(setup.CreateStartingNetwork());
                    sample.Record(evaluator.Evaluate(sample.Genes));
                    best = best == null || sample.Fitness > best.Fitness ? sample : best;
                    if (Solved.Solves(best.Fitness))
                    {
                        return EvolutionSearch.Outcome(SearchStop.Solved, best, request.Budget);
                    }
                }
                return EvolutionSearch.Outcome(SearchStop.BudgetSpent, best, request.Budget);
            }
        }

        private static readonly Lazy<RandomSampling> Registered = new Lazy<RandomSampling>(() =>
        {
            var search = new RandomSampling();
            SearchCatalog.Register(search);
            return search;
        });

        [Fact]
        public void ASearchRegisteredOnceRunsFromTheBenchmarkCommand()
        {
            _ = Registered.Value;
            using var run = new CommandRun();

            string output = run.Run("benchmark", "--algorithm", RandomSampling.SearchName, "--task", "Compute n", "--seeds", "1", "--budget", "40", "--population", "5");

            Assert.Contains("exit 0", output);
            Assert.Contains($"Compute n   {RandomSampling.SearchName}", output);
            Assert.Contains(SearchCatalog.FromScratch, search => search.Name == RandomSampling.SearchName);
        }

        [Fact]
        public void TwoSearchesCannotShareAName()
        {
            Assert.Throws<ArgumentException>(() => SearchCatalog.Register(SearchCatalog.StructuralDefault));
        }

        // The command line looks searches up by name.
        [Fact]
        public void EverySearchHasADistinctName()
        {
            Assert.Equal(SearchCatalog.All.Count, SearchCatalog.All.Select(search => search.Name).Distinct().Count());
        }

        [Fact]
        public void TheBenchmarkComparesOnlySearchesThatStartFromNothing()
        {
            Assert.DoesNotContain(SearchCatalog.FromScratch, search => search.NeedsSeeds);
            Assert.Contains(SearchCatalog.All, search => search.NeedsSeeds);
        }
    }
}
