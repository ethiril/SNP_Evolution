using System;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Tasks;

namespace SnpEvolution.Evolution.Algorithms
{
    // The one place a search decides its best candidate solves the task: its score says so and a more thorough check
    // confirms it. A failed check's score becomes the candidate's, since an elite kept with a lucky score is never
    // rescored and would fail every check after.
    public static class SolveCheck
    {
        public static bool Confirms(Individual best, FitnessEvaluator evaluator) =>
            Confirms(best.Fitness, () => evaluator.ConfirmSolved(best.Genes).Failed, best.Record);

        // failedCheck gives the more thorough check's result when it does not solve the task, or null when it does.
        public static bool Confirms<TResult>(float fitness, Func<TResult?> failedCheck, Action<TResult> record)
            where TResult : class
        {
            if (!Solved.Solves(fitness))
            {
                return false;
            }
            if (failedCheck() is not TResult failed)
            {
                return true;
            }
            record(failed);
            return false;
        }
    }
}
