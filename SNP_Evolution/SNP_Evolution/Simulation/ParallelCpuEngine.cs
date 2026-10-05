using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SnpEvolution.Simulation
{
    // Samples the trials across all cores. Each trial's runs are split into chunks so that even a batch of a few large
    // networks keeps every core busy. Random is not thread-safe, so each chunk gets its own generator, seeded up front
    // from the given random so the results depend neither on thread scheduling nor on the number of cores.
    public sealed class ParallelCpuEngine : ISimulationEngine
    {
        // A whole number of these runs makes the first chunk exactly the runs after which a silent trial is given up.
        private const int RunsPerChunk = NetworkRunner.SilentRunsBeforeGivingUp;

        public IReadOnlyList<TrialResult> Run(IReadOnlyList<Trial> trials, SimulationOptions options, Random random)
        {
            int[] seeds = Seeds(trials.Count, random);
            int chunkCount = Math.Max(1, (options.Repetitions + RunsPerChunk - 1) / RunsPerChunk);
            var chunks = new TrialResult?[trials.Count, chunkCount];
            RunChunks(Enumerable.Range(0, trials.Count).Select(trial => (trial, 0)).ToList());

            // As NetworkRunner.Sample does, a trial whose first runs read no output at all is given up on.
            RunChunks(Enumerable.Range(0, trials.Count)
                .Where(trial => trials[trial].Readout != Readout.Output || chunks[trial, 0]!.Outputs.Count > 0)
                .SelectMany(trial => Enumerable.Range(1, chunkCount - 1).Select(chunk => (trial, chunk)))
                .ToList());

            return Enumerable.Range(0, trials.Count)
                .Select(trial => NetworkRunner.Merge(Enumerable.Range(0, chunkCount).Select(chunk => chunks[trial, chunk]).OfType<TrialResult>()))
                .ToList();

            void RunChunks(List<(int Trial, int Chunk)> work) =>
                Parallel.ForEach(work, item =>
                {
                    int runs = Math.Min(RunsPerChunk, options.Repetitions - item.Chunk * RunsPerChunk);
                    var chunkRandom = new Random(unchecked(seeds[item.Trial] + item.Chunk * 0x5BD1E995));
                    chunks[item.Trial, item.Chunk] = NetworkRunner.SampleRuns(trials[item.Trial], options, runs, chunkRandom);
                });
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
