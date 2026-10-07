using SnpEvolution.Model;
using SnpEvolution.Search.Fitness;

namespace SnpEvolution.Tests.Fixtures
{
    // Scores networks with a given function and keeps every network an algorithm asked to have scored.
    internal sealed class RecordingEvaluator : IPopulationEvaluator
    {
        private readonly Func<Network, float[]>? checks;

        public RecordingEvaluator(Func<Network, float> fitness, Func<Network, float[]>? checks = null)
        {
            Fitness = fitness;
            this.checks = checks;
        }

        public Func<Network, float> Fitness { get; set; }

        public List<Network> Seen { get; } = new List<Network>();

        public IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks)
        {
            Seen.AddRange(networks);
            return networks.Select(network => new FitnessResult(Fitness(network), Array.Empty<int>(), Checks: checks?.Invoke(network))).ToList();
        }
    }
}
