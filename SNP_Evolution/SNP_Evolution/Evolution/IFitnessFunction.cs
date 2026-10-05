using System.Collections.Generic;

namespace SnpEvolution.Evolution
{
    // Scores a network's sorted outputs from 0 (useless) to 1 (solved); FitnessEvaluator.SolvedThreshold applies to every function.
    public interface IFitnessFunction
    {
        float Score(IReadOnlyList<int> outputs);
    }
}
