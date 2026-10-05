using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Tasks;

namespace SnpEvolution.Evolution
{
    // What a population can and cannot do yet, read from the task's checks: which checks some network gets right,
    // which none does, and the first of those, the frontier, which is what a stalled run is missing. Following
    // Krawiec's behavioural program synthesis, a check nobody passes points at the part to build next.
    public sealed record CheckDiagnosis(int Checks, IReadOnlyList<int> Unsolved, int? Frontier, IReadOnlyList<float> BestScores)
    {
        // A check counts as done when some network gets it right on almost every run.
        public const float SolvedCheck = 0.95f;

        public static CheckDiagnosis Of(IReadOnlyList<Individual> population)
        {
            List<Individual> scored = population.Where(individual => individual.Checks.Count > 0).ToList();
            int checks = scored.Count == 0 ? 0 : scored.Min(individual => individual.Checks.Count);
            List<float> best = Enumerable.Range(0, checks).Select(check => scored.Max(individual => individual.Checks[check])).ToList();
            List<int> unsolved = Enumerable.Range(0, checks).Where(check => best[check] < SolvedCheck).ToList();
            return new CheckDiagnosis(checks, unsolved, unsolved.Count > 0 ? unsolved[0] : null, best);
        }

        public string Describe(ITask task)
        {
            if (Checks == 0)
            {
                return "The task does not split into checks, so there is nothing to diagnose.";
            }
            if (Frontier is not int frontier)
            {
                return $"Every one of the {Checks} checks is done by some network, but no network does them all.";
            }
            string rest = Unsolved.Count > 1 ? $", and {Unsolved.Count - 1} more after it" : "";
            return $"No network yet does {task.CheckName(frontier)} (closest {BestScores[frontier]:0.00}){rest}; {Checks - Unsolved.Count} of {Checks} checks are done somewhere.";
        }
    }
}
