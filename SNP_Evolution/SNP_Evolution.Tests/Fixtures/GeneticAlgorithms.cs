using SnpEvolution.Model;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Operators;

namespace SnpEvolution.Tests.Fixtures
{
    internal static class GeneticAlgorithms
    {
        // A generational algorithm with tournament selection and neuron crossover, so a test picks only the mutation.
        public static GeneticAlgorithm Generational(int size, Random random, Func<Network> create, IPopulationEvaluator evaluator, IMutation mutation) =>
            new GeneticAlgorithm(size, random, create, evaluator, new GeneticOperators(new TournamentSelection(3), new NeuronCrossover(), mutation), 1, _ => { });
    }
}
