using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Search.Fitness;

namespace SnpEvolution.Search.Algorithms
{
    // One row per scored generation, holding every in-range fitness it scored, for the fitness chart a run saves.
    public sealed class ScoreHistory
    {
        private readonly List<IReadOnlyList<float>> rows = new List<IReadOnlyList<float>>();

        public IReadOnlyList<IReadOnlyList<float>> Rows => rows;

        public static bool IsRecordable(float fitness) => fitness >= 0 && fitness <= 1;

        public void Record(IEnumerable<Individual> scored) => rows.Add(scored.Select(individual => individual.Fitness).Where(IsRecordable).ToList());
    }
}
