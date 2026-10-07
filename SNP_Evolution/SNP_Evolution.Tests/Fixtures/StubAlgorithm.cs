using SnpEvolution.Model;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;

namespace SnpEvolution.Tests.Fixtures
{
    // A fixed population that records what it is given, so the modular loop can be watched without evolving.
    internal sealed class StubAlgorithm : IGeneticAlgorithm
    {
        public List<Individual> Individuals { get; } = new List<Individual>();

        public List<Network> Immigrants { get; } = new List<Network>();

        public int Rescores { get; private set; }

        public IReadOnlyList<Individual> Population => Individuals;

        public int Generation { get; private set; } = 1;

        public Individual? Best => Individuals.Count == 0 ? null : Ranking.Rank(Individuals)[0];

        public IReadOnlyList<IReadOnlyList<float>> FitnessHistory => Array.Empty<IReadOnlyList<float>>();

        public void NextGeneration() => Generation++;

        public void Immigrate(IReadOnlyList<Network> newcomers) => Immigrants.AddRange(newcomers);

        public void Rescore() => Rescores++;
    }
}
