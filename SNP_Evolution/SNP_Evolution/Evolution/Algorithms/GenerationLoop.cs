using System;
using System.Threading;
using SnpEvolution.Evolution.Accounting;

namespace SnpEvolution.Evolution.Algorithms
{
    // The one loop every search runs: a generation at a time until a generation solves the task, the budget or the
    // generations run out, or the search is cancelled. The budget is looked at before each generation, so one started
    // is always finished.
    public static class GenerationLoop
    {
        // generation runs one generation, given how many came before, and says whether the search is now solved.
        // Generations is how many were run.
        public static (SearchStop Stop, int Generations) Run(int maxGenerations, Func<bool> budgetSpent, CancellationToken cancellation, Func<int, bool> generation)
        {
            for (int run = 0; run < maxGenerations; run++)
            {
                if (cancellation.IsCancellationRequested)
                {
                    return (SearchStop.Cancelled, run);
                }
                if (budgetSpent())
                {
                    return (SearchStop.BudgetSpent, run);
                }
                if (generation(run))
                {
                    return (SearchStop.Solved, run + 1);
                }
            }
            return (SearchStop.BudgetSpent, maxGenerations);
        }

        public static (SearchStop Stop, int Generations) Run(IGeneticAlgorithm algorithm, int maxGenerations, Func<bool> budgetSpent, Func<IGeneticAlgorithm, bool> solved,
            CancellationToken cancellation = default) =>
            Run(maxGenerations, budgetSpent, cancellation, _ =>
            {
                algorithm.NextGeneration();
                return solved(algorithm);
            });
    }
}
