using System;

namespace SnpEvolution.Simulation
{
    // What every sampling engine shares: one seed per trial drawn up front, so results depend neither on scheduling nor
    // on the hardware, and giving up on a trial whose opening runs read no output at all, as it is unlikely ever to.
    internal static class Sampling
    {
        public const int OpeningRuns = 7;

        public static int Opening(SimulationOptions options) => Math.Min(options.Repetitions, OpeningRuns);

        public static bool GivesUp(Trial trial, bool readAnOutput) => trial.Readout == Readout.Output && !readAnOutput;

        public static int[] Seeds(int count, Random random)
        {
            var seeds = new int[count];
            for (int index = 0; index < seeds.Length; index++)
            {
                seeds[index] = random.Next();
            }
            return seeds;
        }
    }
}
