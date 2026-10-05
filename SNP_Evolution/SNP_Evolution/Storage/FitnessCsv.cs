using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SnpEvolution.Storage
{
    public static class FitnessCsv
    {
        public static string Format(IEnumerable<IEnumerable<float>> fitnessHistory) =>
            string.Concat(fitnessHistory.Select(generation =>
                string.Join(",", generation.Select(fitness => fitness.ToString(CultureInfo.InvariantCulture))) + "\n"));
    }
}
