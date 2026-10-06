using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Fitness;

namespace SnpEvolution.Evolution.Algorithms
{
    // Fitter first and, between equally fit networks, smaller first: parsimony pressure that steers the search
    // towards the small systems the field values without ever trading away fitness.
    public static class Ranking
    {
        public static readonly IComparer<Individual> FittestFirst = Comparer<Individual>.Create(Compare);

        public static int Compare(Individual first, Individual second)
        {
            int byFitness = Score(second.Fitness).CompareTo(Score(first.Fitness));
            return byFitness != 0 ? byFitness : first.Genes.Size.CompareTo(second.Genes.Size);
        }

        public static bool IsBetter(Individual candidate, Individual incumbent) => Compare(candidate, incumbent) < 0;

        // A stable sort, so among exact ties earlier individuals stay ahead.
        public static List<Individual> Rank(IEnumerable<Individual> individuals) => individuals.OrderBy(individual => individual, FittestFirst).ToList();

        // Out-of-range fitness ranks below everything else.
        private static float Score(float fitness) => fitness >= 0 && fitness <= 1 ? fitness : -1;
    }

    public static class Evaluation
    {
        public static void Evaluate(IPopulationEvaluator evaluator, IReadOnlyList<Individual> individuals)
        {
            IReadOnlyList<FitnessResult> results = evaluator.EvaluateAll(individuals.Select(individual => individual.Genes).ToList());
            for (int index = 0; index < individuals.Count; index++)
            {
                individuals[index].Record(results[index]);
            }
        }
    }
}
