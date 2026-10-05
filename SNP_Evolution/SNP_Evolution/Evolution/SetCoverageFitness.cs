using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Evolution
{
    // An F1-style score against the expected set, scaled by how much of that set the outputs cover.
    public sealed class SetCoverageFitness : IFitnessFunction
    {
        private readonly IReadOnlyCollection<int> expectedSet;

        public SetCoverageFitness(IReadOnlyCollection<int> expectedSet)
        {
            this.expectedSet = expectedSet;
        }

        public float Score(IReadOnlyList<int> outputs)
        {
            if (outputs.Count == 0)
            {
                return 0;
            }
            int expectedCount = expectedSet.Count;
            int hits = outputs.Count(expectedSet.Contains);
            int distinctHits = outputs.Where(expectedSet.Contains).Distinct().Count();

            // Weighting every count by the expected-set size reproduces the fitness used for the published runs.
            float truePositives = Normalise(hits * expectedCount, expectedCount, outputs.Count);
            float falsePositives = Normalise((outputs.Count - hits) * expectedCount, expectedCount, outputs.Count);
            float falseNegatives = expectedSet.Except(outputs).Count();
            float coverage = (float)distinctHits / expectedCount;

            return 2 * truePositives / (2 * truePositives + falsePositives + falseNegatives) * coverage;
        }

        private static float Normalise(float weightedCount, int expectedCount, int outputCount) =>
            weightedCount > expectedCount ? (weightedCount - expectedCount) / (outputCount - expectedCount) : 0;
    }
}
