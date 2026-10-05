using System.Collections.Generic;

namespace SnpEvolution.Evolution
{
    // What the console needs from an evolution strategy, so a different algorithm can be dropped in.
    public interface IGeneticAlgorithm
    {
        IReadOnlyList<Individual> Population { get; }

        // Starts at 1 and advances after each call to NextGeneration.
        int Generation { get; }

        Individual? Best { get; }

        // One row per evaluated generation, holding the in-range fitnesses it scored.
        IReadOnlyList<IReadOnlyList<float>> FitnessHistory { get; }

        void NextGeneration();
    }
}
