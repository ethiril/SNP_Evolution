using SnpEvolution.Simulation;

namespace SnpEvolution.Tests.Fixtures
{
    internal static class TrialResults
    {
        // A sampled result that recorded these spike trains and no outputs.
        public static TrialResult Trains(params int[][] trains) => new TrialResult(Array.Empty<int>(), false, TrialCoverage.Sampled, trains);
    }
}
