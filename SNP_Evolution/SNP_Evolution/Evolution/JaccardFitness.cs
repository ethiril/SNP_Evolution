using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Evolution
{
    // How much the distinct outputs and the expected set overlap, ignoring how often each output occurs.
    public sealed class JaccardFitness : IFitnessFunction
    {
        private readonly HashSet<int> expectedSet;

        public JaccardFitness(IEnumerable<int> expectedSet)
        {
            this.expectedSet = expectedSet.ToHashSet();
        }

        public float Score(IReadOnlyList<int> outputs)
        {
            var distinct = outputs.ToHashSet();
            int union = distinct.Union(expectedSet).Count();
            return union == 0 ? 0 : (float)distinct.Intersect(expectedSet).Count() / union;
        }
    }
}
