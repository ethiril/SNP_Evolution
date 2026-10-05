using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Simulation.Metal;

namespace SnpEvolution.Simulation
{
    // Samples on the GPU through Metal, every repetition of every trial at once, each in its own threadgroup. Rule
    // choices come from a hash of the run's seed, the step and the neuron rather than from System.Random, so results
    // differ from the CPU engines' run for run but follow the same distribution, and the same seed repeats them.
    // Batches too small to repay a GPU round trip run on the CPU instead, as do networks beyond the kernel's limits
    // and Ports readouts.
    public sealed class MetalEngine : ISimulationEngine
    {
        // In neuron-steps: every trial's neurons times MaxSteps times Repetitions.
        public const long DefaultGpuThreshold = 1_000_000;

        private readonly MetalContext context;
        private readonly long gpuThreshold;
        private readonly ParallelCpuEngine cpu = new ParallelCpuEngine();

        public MetalEngine(long gpuThreshold = DefaultGpuThreshold)
        {
            context = MetalContext.Shared ?? throw new PlatformNotSupportedException("This machine has no Metal GPU.");
            this.gpuThreshold = gpuThreshold;
        }

        public static bool IsAvailable => MetalContext.HasDevice;

        public string DeviceName => context.DeviceName;

        public IReadOnlyList<TrialResult> Run(IReadOnlyList<Trial> trials, SimulationOptions options, Random random)
        {
            long work = trials.Sum(trial => (long)CompiledNetwork.Of(trial.Network).NeuronCount) * options.MaxSteps * options.Repetitions;
            if (work < gpuThreshold)
            {
                return cpu.Run(trials, options, random);
            }
            int[] seeds = ParallelCpuEngine.Seeds(trials.Count, random);
            var results = new TrialResult[trials.Count];
            var runs = new List<GpuRunResult>[trials.Count];
            var onGpu = new List<int>();
            var onCpu = new List<int>();
            for (int index = 0; index < trials.Count; index++)
            {
                runs[index] = new List<GpuRunResult>();
                // The kernel has no Ports readout; contracts are checked on the CPU.
                bool supported = trials[index].Readout != Readout.Ports && GpuNetwork.Of(CompiledNetwork.Of(trials[index].Network)).IsSupported;
                (supported ? onGpu : onCpu).Add(index);
            }
            if (onCpu.Count > 0)
            {
                IReadOnlyList<TrialResult> sampled = cpu.Run(onCpu.Select(index => trials[index]).ToList(), options, new Random(seeds[onCpu[0]]));
                for (int position = 0; position < onCpu.Count; position++)
                {
                    results[onCpu[position]] = sampled[position];
                }
            }

            // As NetworkRunner.Sample does, a trial whose first runs read no output at all is given up on.
            int opening = Math.Min(options.Repetitions, NetworkRunner.SilentRunsBeforeGivingUp);
            RunRepetitions(onGpu, 0, opening);
            RunRepetitions(onGpu.Where(index => trials[index].Readout != Readout.Output || runs[index].Any(run => run.Output != null)).ToList(), opening, options.Repetitions);
            foreach (int index in onGpu)
            {
                results[index] = Result(trials[index].Readout, runs[index]);
            }
            return results;

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
            return new TrialResult(outputs, runs.Any(run => run.Halted), Exact: false, spikeTrains);
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
