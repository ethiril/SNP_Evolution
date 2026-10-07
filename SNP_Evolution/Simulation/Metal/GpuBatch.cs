using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using SnpEvolution.Model;

namespace SnpEvolution.Simulation.Metal
{
    // A batch of runs laid out in the kernel's buffers: each distinct network and trial input once, and a state and
    // spike train slice per run. NeuronCount sums every run's neurons, the per-step work a dispatch is sized by.
    internal sealed unsafe record GpuBatch(int[] TrainBase, int TrainTotal, uint NeuronCount, int ThreadsPerGroup)
    {
        public static GpuBatch Fill(MetalBuffers buffers, IReadOnlyList<GpuRun> runs, SimulationOptions options, int maxThreadsPerGroup)
        {
            var networkIndex = new Dictionary<Network, int>(ReferenceEqualityComparer.Instance);
            var networks = new List<GpuNetwork>();
            var trialIndex = new Dictionary<Trial, int>(ReferenceEqualityComparer.Instance);
            var trials = new List<Trial>();
            foreach (GpuRun run in runs)
            {
                if (trialIndex.TryAdd(run.Trial, trials.Count))
                {
                    trials.Add(run.Trial);
                }
                if (networkIndex.TryAdd(run.Trial.Network, networks.Count))
                {
                    networks.Add(GpuNetwork.Of(CompiledNetwork.Of(run.Trial.Network)));
                }
            }

            Span<NetworkDesc> descs = buffers.Buffer<NetworkDesc>(MetalBuffers.Slot.Networks, networks.Count);
            Span<GpuNeuron> neurons = buffers.Buffer<GpuNeuron>(MetalBuffers.Slot.Neurons, networks.Sum(network => network.Neurons.Length));
            Span<GpuRule> rules = buffers.Buffer<GpuRule>(MetalBuffers.Slot.Rules, networks.Sum(network => network.Rules.Length));
            Span<byte> accepts = buffers.Buffer<byte>(MetalBuffers.Slot.Accepts, networks.Sum(network => network.Accepts.Length));
            Span<uint> incoming = buffers.Buffer<uint>(MetalBuffers.Slot.Incoming, networks.Sum(network => network.Incoming.Length));
            var desc = new NetworkDesc();
            for (int index = 0; index < networks.Count; index++)
            {
                GpuNetwork network = networks[index];
                descs[index] = desc with { NeuronCount = (uint)network.Neurons.Length, OutputCount = (uint)network.OutputCount };
                network.Neurons.CopyTo(neurons[(int)desc.NeuronBase..]);
                network.Rules.CopyTo(rules[(int)desc.RuleBase..]);
                network.Accepts.CopyTo(accepts[(int)desc.AcceptBase..]);
                network.Incoming.CopyTo(incoming[(int)desc.IncomingBase..]);
                desc = new NetworkDesc
                {
                    NeuronBase = desc.NeuronBase + (uint)network.Neurons.Length,
                    RuleBase = desc.RuleBase + (uint)network.Rules.Length,
                    AcceptBase = desc.AcceptBase + (uint)network.Accepts.Length,
                    IncomingBase = desc.IncomingBase + (uint)network.Incoming.Length,
                };
            }

            // Each trial's input: for input k, the steps a spike arrives on are inputSteps[offsets[base + k]] up to
            // inputSteps[offsets[base + k + 1]].
            var inputBase = new int[trials.Count];
            var inputCount = new int[trials.Count];
            var offsetList = new List<uint>();
            var stepList = new List<int>();
            for (int index = 0; index < trials.Count; index++)
            {
                Trial trial = trials[index];
                inputBase[index] = offsetList.Count;
                inputCount[index] = Math.Min(CompiledNetwork.Of(trial.Network).inputNeurons.Length, trial.Input.StepsPerInput.Count);
                for (int input = 0; input < inputCount[index]; input++)
                {
                    offsetList.Add((uint)stepList.Count);
                    stepList.AddRange(trial.Input.StepsPerInput[input]);
                }
                offsetList.Add((uint)stepList.Count);
            }
            offsetList.ToArray().CopyTo(buffers.Buffer<uint>(MetalBuffers.Slot.InputOffsets, offsetList.Count));
            stepList.ToArray().CopyTo(buffers.Buffer<int>(MetalBuffers.Slot.InputSteps, stepList.Count));

            Span<GpuSim> sims = buffers.Buffer<GpuSim>(MetalBuffers.Slot.Sims, runs.Count);
            Span<Progress> progress = buffers.Buffer<Progress>(MetalBuffers.Slot.Progress, runs.Count);
            var trainBase = new int[runs.Count];
            uint stateBase = 0;
            int trainTotal = 0;
            int maxNeurons = 0;
            for (int index = 0; index < runs.Count; index++)
            {
                Trial trial = runs[index].Trial;
                int network = networkIndex[trial.Network];
                int trialAt = trialIndex[trial];
                int neuronCount = networks[network].Neurons.Length;
                int trainCapacity = trial.Readout == Readout.SpikeTrain ? options.MaxSteps * networks[network].OutputCount : 0;
                trainBase[index] = trainTotal;
                sims[index] = new GpuSim
                {
                    Network = (uint)network,
                    StateBase = stateBase,
                    Seed = runs[index].Seed,
                    InputBase = (uint)inputBase[trialAt],
                    InputCount = (uint)inputCount[trialAt],
                    LastInputStep = trial.Input.LastStep,
                    TrainBase = (uint)trainTotal,
                    TrainCapacity = (uint)trainCapacity,
                    Readout = (uint)trial.Readout,
                };
                progress[index] = new Progress { Output = -1 };
                stateBase += (uint)neuronCount;
                trainTotal += trainCapacity;
                maxNeurons = Math.Max(maxNeurons, neuronCount);
            }
            buffers.Buffer<NeuronState>(MetalBuffers.Slot.States, (int)stateBase);
            buffers.Buffer<short>(MetalBuffers.Slot.Emitting, 2 * (int)stateBase);
            buffers.Buffer<int>(MetalBuffers.Slot.Trains, trainTotal);

            int threads = 32;
            while (threads < maxNeurons && threads < maxThreadsPerGroup)
            {
                threads *= 2;
            }
            return new GpuBatch(trainBase, trainTotal, stateBase, Math.Min(threads, maxThreadsPerGroup));
        }

        public GpuRunResult[] Collect(MetalBuffers buffers, IReadOnlyList<GpuRun> runs)
        {
            Span<Progress> progress = buffers.View<Progress>(MetalBuffers.Slot.Progress, runs.Count);
            Span<GpuSim> sims = buffers.View<GpuSim>(MetalBuffers.Slot.Sims, runs.Count);
            Span<int> trains = buffers.View<int>(MetalBuffers.Slot.Trains, TrainTotal);
            var results = new GpuRunResult[runs.Count];
            for (int index = 0; index < runs.Count; index++)
            {
                Progress run = progress[index];
                int recorded = (int)Math.Min(run.TrainCount, sims[index].TrainCapacity);
                results[index] = new GpuRunResult(
                    run.Output >= 0 ? run.Output : null,
                    run.Halted != 0,
                    trains.Slice(TrainBase[index], recorded).ToArray());
            }
            return results;
        }
    }

    // The layouts below match the structs of the same names in Simulation.metal field for field.
    [StructLayout(LayoutKind.Sequential)]
    internal record struct NetworkDesc
    {
        public uint NeuronBase;
        public uint NeuronCount;
        public uint RuleBase;
        public uint AcceptBase;
        public uint IncomingBase;
        public uint OutputCount;
        public uint Pad0;
        public uint Pad1;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuSim
    {
        public uint Network;
        public uint StateBase;
        public uint Seed;
        public uint InputBase;
        public uint InputCount;
        public int LastInputStep;
        public uint TrainBase;
        public uint TrainCapacity;
        public uint Readout;
        public uint Pad0;
        public uint Pad1;
        public uint Pad2;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Progress
    {
        public int Step;
        public int OutputCounter;
        public int OutputEngaged;
        public int Output;
        public int Halted;
        public int Finished;
        public int Started;
        public uint TrainCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NeuronState
    {
        public long Spikes;
        public ushort LegacyDelay;
        public ushort ClosedFor;
        public ushort PendingEmission;
        public byte LegacyPending;
        public byte Pad;
    }
}
