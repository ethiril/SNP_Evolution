using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SnpEvolution.Simulation
{
    // Samples the trials across all cores. Random is not thread-safe, so each trial gets its own generator,
    // seeded up front from the given random so the results do not depend on thread scheduling.
    public sealed class ParallelCpuEngine : ISimulationEngine
    {
        public IReadOnlyList<TrialResult> Run(IReadOnlyList<Trial> trials, SimulationOptions options, Random random)
        {
            int[] seeds = Seeds(trials.Count, random);
            var results = new TrialResult[trials.Count];
            Parallel.For(0, trials.Count, index => results[index] = NetworkRunner.Sample(trials[index], options, new Random(seeds[index])));
            return results;
        }

        internal static int[] Seeds(int count, Random random)
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
