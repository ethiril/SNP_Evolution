using System.Collections.Generic;
using SnpEvolution.Model;
using SnpEvolution.Search.Fitness;

namespace SnpEvolution.Search.Algorithms
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

        // Puts the networks into the next generation in place of its weakest members, never the best, so a stalled
        // search gets fresh material. They are scored along with the rest of that generation.
        void Immigrate(IReadOnlyList<Network> newcomers);

        // The task has changed, so any network kept with a score from before is scored again before it competes,
        // and anything remembered about earlier fitness is forgotten.
        void Rescore();
    }
}
