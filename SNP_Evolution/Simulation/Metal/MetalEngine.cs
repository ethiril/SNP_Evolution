using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Simulation.Metal;

namespace SnpEvolution.Simulation.Metal
{
    // Samples on the GPU through Metal, every repetition of every trial at once, each in its own threadgroup. Rule
    // choices come from a hash of the run's seed, the step and the neuron rather than from System.Random, so results
    // differ from the CPU engines' run for run but follow the same distribution, and the same seed repeats them.
    // The kernel has no jitter, axon or Ports readout and keeps delays and emissions in 16 bits (see Support); OrCpu
    // sends what it cannot run, and batches too small to repay a GPU round trip, to the CPU.
    public sealed class MetalEngine : ISimulationEngine
    {
        // In neuron-steps: every trial's neurons times MaxSteps times Repetitions.
        public const long DefaultGpuThreshold = 1_000_000;

        private readonly MetalContext context;

        public MetalEngine()
        {
            context = MetalContext.Shared ?? throw new PlatformNotSupportedException("This machine has no Metal GPU.");
        }

        public static bool IsAvailable => MetalContext.HasDevice;

        public string DeviceName => context.DeviceName;

        public EngineSupport Support { get; } = new EngineSupport(Jitter: false, AxonalDelay: false, Ports: false, EveryComputation: false,
            GpuNetwork.MaxOutputs, GpuNetwork.MaxSmallValue, short.MaxValue);

        // The GPU for the trials and batches it suits, and every core of the CPU for the rest.
        public static ISimulationEngine OrCpu(long gpuThreshold = DefaultGpuThreshold) => new RoutedEngine(new MetalEngine(), new ParallelCpuEngine(), gpuThreshold);

        public IReadOnlyList<TrialResult> Run(IReadOnlyList<Trial> trials, SimulationOptions options, Random random)
        {
            int[] seeds = Sampling.Seeds(trials.Count, random);
            var runs = new List<GpuRunResult>[trials.Count];
            var onGpu = new List<int>();
            for (int index = 0; index < trials.Count; index++)
            {
                runs[index] = new List<GpuRunResult>();
                if (Support.Runs(trials[index], options))
                {
                    onGpu.Add(index);
                }
            }

            int opening = Sampling.Opening(options);
            RunRepetitions(onGpu, 0, opening);
            RunRepetitions(onGpu.Where(index => !Sampling.GivesUp(trials[index], runs[index].Any(run => run.Output != null))).ToList(), opening, options.Repetitions);
            return Enumerable.Range(0, trials.Count)
                .Select(index => onGpu.Contains(index) ? Result(trials[index].Readout, runs[index]) : TrialResult.Unsupported)
                .ToList();

            void RunRepetitions(List<int> trialIndexes, int from, int to)
            {
                var requested = new List<GpuRun>();
                var owners = new List<int>();
                foreach (int index in trialIndexes)
                {
                    for (int repetition = from; repetition < to; repetition++)
                    {
                        requested.Add(new GpuRun(trials[index], Seed(seeds[index], repetition)));
                        owners.Add(index);
                    }
                }
                GpuRunResult[] completed = context.Run(requested, options);
                for (int run = 0; run < completed.Length; run++)
                {
                    runs[owners[run]].Add(completed[run]);
                }
            }
        }

        private static TrialResult Result(Readout readout, List<GpuRunResult> runs)
        {
            List<int> outputs = readout == Readout.Halting
                ? new List<int>()
                : runs.Where(run => run.Output != null).Select(run => run.Output!.Value).OrderBy(output => output).ToList();
            IReadOnlyList<IReadOnlyList<int>>? spikeTrains = readout == Readout.SpikeTrain ? runs.Select(run => (IReadOnlyList<int>)run.SpikeSteps).ToList() : null;
            return new TrialResult(outputs, runs.Any(run => run.Halted), TrialCoverage.Sampled, spikeTrains);
        }

        // Spreads the trial's seed over its repetitions; the kernel mixes it further with the step and neuron.
        private static uint Seed(int trialSeed, int repetition)
        {
            uint x = (uint)trialSeed * 0x9E3779B1u + (uint)repetition * 0x85EBCA77u;
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            return x ^ (x >> 16);
        }
    }
}
