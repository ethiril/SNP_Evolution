using SnpEvolution.Model;
using SnpEvolution.Search.Fitness;

namespace SnpEvolution.Tests.Fixtures
{
    // Scores each network with the function it is given.
    internal sealed class DelegateEvaluator : IPopulationEvaluator
    {
        private readonly Func<Network, FitnessResult> evaluate;

        public DelegateEvaluator(Func<Network, FitnessResult> evaluate) => this.evaluate = evaluate;

        public IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks) => networks.Select(evaluate).ToList();
    }
}
