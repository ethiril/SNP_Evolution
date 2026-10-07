using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Fitness;

namespace SnpEvolution.Evolution.Algorithms
{
    // Fitter first and, between equally fit candidates, smaller first: parsimony pressure that steers the search
    // towards the small systems the field values without ever trading away fitness.
    public static class Ranking
    {
        public static readonly IComparer<Individual> FittestFirst = Comparer<Individual>.Create(Compare);

        public static int Compare(IScored first, IScored second)
        {
            int byFitness = Score(second.Fitness).CompareTo(Score(first.Fitness));
            return byFitness != 0 ? byFitness : first.Size.CompareTo(second.Size);
        }

        public static bool IsBetter(IScored candidate, IScored incumbent) => Compare(candidate, incumbent) < 0;

        // A stable sort, so among exact ties earlier candidates stay ahead.
        public static List<T> Rank<T>(IEnumerable<T> candidates) where T : IScored =>
            candidates.OrderBy(candidate => candidate, Comparer<T>.Create((first, second) => Compare(first, second))).ToList();

        // Out-of-range fitness ranks below everything else.
        private static float Score(float fitness) => ScoreHistory.IsRecordable(fitness) ? fitness : -1;
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
