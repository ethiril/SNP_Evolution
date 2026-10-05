using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SnpEvolution.Networks;
using static SnpEvolution.Simulation.Metal.ObjectiveC;

namespace SnpEvolution.Simulation.Metal
{
    // One sampled computation for the GPU to run: a trial, which repetition of it this is, and that run's seed.
    internal readonly record struct GpuRun(Trial Trial, uint Seed);

    // What one run ended with. SpikeSteps is empty unless the trial reads a spike train.
    internal readonly record struct GpuRunResult(int? Output, bool Halted, int[] SpikeSteps);

    // The Metal device, the compiled kernel and the buffers it runs over, shared by the whole process. Buffers live in
    // memory the CPU and GPU share, so filling one is a plain write and nothing is copied.
    internal sealed unsafe class MetalContext
    {
        private const string KernelResource = "SnpEvolution.Simulation.Metal.Simulation.metal";

        // How long one command buffer should keep the GPU busy: long enough that the round trip does not matter, short
        // enough to stay well clear of the watchdog that stops long-running GPU work.
        private const double TargetDispatchSeconds = 0.05;

        private static readonly Lazy<MetalContext?> Instance = new Lazy<MetalContext?>(Create);

        private readonly object gate = new object();
        private readonly IntPtr device;
        private readonly IntPtr queue;
        private readonly IntPtr pipeline;
        private readonly int maxThreadsPerGroup;
        private readonly Dictionary<int, SharedBuffer> buffers = new Dictionary<int, SharedBuffer>();
        // Measured from the last dispatch and used to size the next, so each command buffer lasts about the target.
        private double secondsPerNeuronStep = 1e-9;

        private MetalContext(IntPtr device, IntPtr queue, IntPtr pipeline)
        {
            this.device = device;
            this.queue = queue;
            this.pipeline = pipeline;
            maxThreadsPerGroup = (int)SendForUnsigned(pipeline, Selector("maxTotalThreadsPerThreadgroup"));
        }

        // Null when this machine has no Metal device.
        public static MetalContext? Shared => Instance.Value;

        // Whether there is a Metal device, without compiling the kernel.
        public static bool HasDevice { get; } = CreateDefaultDevice() != IntPtr.Zero;

        public string DeviceName => Marshal.PtrToStringUTF8(Send(Send(device, Selector("name")), Selector("UTF8String"))) ?? "GPU";

        public GpuRunResult[] Run(IReadOnlyList<GpuRun> runs, SimulationOptions options)
        {
            if (runs.Count == 0)
            {
                return Array.Empty<GpuRunResult>();
            }
            lock (gate)
            {
                Layout layout = Fill(runs, options);
                Execute(runs.Count, layout, options);
                return Collect(runs, layout);
            }
        }

        private static MetalContext? Create()
        {
            IntPtr device = HasDevice ? CreateDefaultDevice() : IntPtr.Zero;
            if (device == IntPtr.Zero)
            {
                return null;
            }
            using Stream stream = typeof(MetalContext).Assembly.GetManifestResourceStream(KernelResource)
                ?? throw new InvalidOperationException($"The {KernelResource} resource is missing.");
            string source = new StreamReader(stream).ReadToEnd();
            IntPtr library = Send(device, Selector("newLibraryWithSource:options:error:"), NSString(source), IntPtr.Zero, out IntPtr error);
            if (library == IntPtr.Zero)
            {
                throw new InvalidOperationException("The Metal kernel did not compile: " + Describe(error));
            }
            IntPtr function = Send(library, Selector("newFunctionWithName:"), NSString("Simulate"));
            IntPtr pipeline = Send(device, Selector("newComputePipelineStateWithFunction:error:"), function, out error);
            if (pipeline == IntPtr.Zero)
            {
                throw new InvalidOperationException("The Metal pipeline could not be built: " + Describe(error));
            }
            return new MetalContext(device, Send(device, Selector("newCommandQueue")), pipeline);
        }

        private Layout Fill(IReadOnlyList<GpuRun> runs, SimulationOptions options)
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

            Span<NetworkDesc> descs = Buffer<NetworkDesc>(Slot.Networks, networks.Count);
            Span<GpuNeuron> neurons = Buffer<GpuNeuron>(Slot.Neurons, networks.Sum(network => network.Neurons.Length));
            Span<GpuRule> rules = Buffer<GpuRule>(Slot.Rules, networks.Sum(network => network.Rules.Length));
            Span<byte> accepts = Buffer<byte>(Slot.Accepts, networks.Sum(network => network.Accepts.Length));
            Span<uint> incoming = Buffer<uint>(Slot.Incoming, networks.Sum(network => network.Incoming.Length));
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
            offsetList.ToArray().CopyTo(Buffer<uint>(Slot.InputOffsets, offsetList.Count));
            stepList.ToArray().CopyTo(Buffer<int>(Slot.InputSteps, stepList.Count));

            Span<GpuSim> sims = Buffer<GpuSim>(Slot.Sims, runs.Count);
            Span<Progress> progress = Buffer<Progress>(Slot.Progress, runs.Count);
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
            Buffer<NeuronState>(Slot.States, (int)stateBase);
            Buffer<short>(Slot.Emitting, 2 * (int)stateBase);
            Buffer<int>(Slot.Trains, trainTotal);

            int threads = 32;
            while (threads < maxNeurons && threads < maxThreadsPerGroup)
            {
                threads *= 2;
            }
            return new Layout(trainBase, trainTotal, stateBase, Math.Min(threads, maxThreadsPerGroup));
        }

        // Runs the kernel a chunk of steps at a time until every run has finished.
        private void Execute(int runCount, Layout layout, SimulationOptions options)
        {
            Span<Progress> progress = View<Progress>(Slot.Progress, runCount);
            bool finished = false;
            while (!finished)
            {
                // A run can take one step beyond MaxSteps, to settle whether it halted.
                double affordable = TargetDispatchSeconds / (secondsPerNeuronStep * layout.NeuronCount);
                int steps = (int)Math.Min(Math.Max(affordable, 8), options.MaxSteps + 1);
                double seconds = Dispatch(runCount, layout.ThreadsPerGroup, steps, options);
                finished = true;
                foreach (Progress run in progress)
                {
                    finished &= run.Finished != 0;
                }
                if (!finished && seconds > 0)
                {
                    secondsPerNeuronStep = seconds / ((double)steps * layout.NeuronCount);
                }
            }
        }

        private double Dispatch(int runCount, int threadsPerGroup, int steps, SimulationOptions options)
        {
            IntPtr pool = PushAutoreleasePool();
            try
            {
                IntPtr commandBuffer = Send(queue, Selector("commandBuffer"));
                IntPtr encoder = Send(commandBuffer, Selector("computeCommandEncoder"));
                Send(encoder, Selector("setComputePipelineState:"), pipeline);
                var parameters = new Params
                {
                    SimCount = (uint)runCount,
                    MaxSteps = options.MaxSteps,
                    Steps = (uint)steps,
                    LegacyTiming = options.Timing == OutputTiming.Legacy ? 1u : 0u,
                };
                Send(encoder, Selector("setBytes:length:atIndex:"), (IntPtr)(&parameters), (nuint)sizeof(Params), 0);
                foreach ((int slot, SharedBuffer buffer) in buffers)
                {
                    Send(encoder, Selector("setBuffer:offset:atIndex:"), buffer.Handle, 0, (nuint)slot);
                }
                Send(encoder, Selector("dispatchThreadgroups:threadsPerThreadgroup:"), new Size((nuint)runCount, 1, 1), new Size((nuint)threadsPerGroup, 1, 1));
                Send(encoder, Selector("endEncoding"));
                Send(commandBuffer, Selector("commit"));
                Send(commandBuffer, Selector("waitUntilCompleted"));
                IntPtr error = Send(commandBuffer, Selector("error"));
                if (error != IntPtr.Zero)
                {
                    throw new InvalidOperationException("The GPU simulation failed: " + Describe(error));
                }
                return SendForDouble(commandBuffer, Selector("GPUEndTime")) - SendForDouble(commandBuffer, Selector("GPUStartTime"));
            }
            finally
            {
                PopAutoreleasePool(pool);
            }
        }

        private GpuRunResult[] Collect(IReadOnlyList<GpuRun> runs, Layout layout)
        {
            Span<Progress> progress = View<Progress>(Slot.Progress, runs.Count);
            Span<GpuSim> sims = View<GpuSim>(Slot.Sims, runs.Count);
            Span<int> trains = View<int>(Slot.Trains, layout.TrainTotal);
            var results = new GpuRunResult[runs.Count];
            for (int index = 0; index < runs.Count; index++)
            {
                Progress run = progress[index];
                int recorded = (int)Math.Min(run.TrainCount, sims[index].TrainCapacity);
                results[index] = new GpuRunResult(
                    run.Output >= 0 ? run.Output : null,
                    run.Halted != 0,
                    trains.Slice(layout.TrainBase[index], recorded).ToArray());
            }
            return results;
        }

        // The buffer for the slot, grown to hold at least count items; its old contents are not kept.
        private Span<T> Buffer<T>(Slot slot, int count) where T : unmanaged
        {
            // Metal rejects empty buffers.
            nuint length = (nuint)Math.Max(16, count * sizeof(T));
            if (!buffers.TryGetValue((int)slot, out SharedBuffer? buffer) || buffer.Length < length)
            {
                Release(buffer?.Handle ?? IntPtr.Zero);
                nuint grown = Math.Max(length, (buffer?.Length ?? 0) * 3 / 2);
                IntPtr handle = Send(device, Selector("newBufferWithLength:options:"), grown, 0);
                if (handle == IntPtr.Zero)
                {
                    throw new OutOfMemoryException($"The GPU could not allocate {grown} bytes.");
                }
                buffer = new SharedBuffer(handle, grown);
                buffers[(int)slot] = buffer;
            }
            return View<T>(slot, count);
        }

        private Span<T> View<T>(Slot slot, int count) where T : unmanaged =>
            new Span<T>((void*)Send(buffers[(int)slot].Handle, Selector("contents")), count);

        private sealed record SharedBuffer(IntPtr Handle, nuint Length);

        // NeuronCount sums every run's neurons, the per-step work the dispatch is sized by.
        private sealed record Layout(int[] TrainBase, int TrainTotal, uint NeuronCount, int ThreadsPerGroup);

        // The kernel's buffer indexes.
        private enum Slot
        {
            Networks = 1,
            Sims = 2,
            Progress = 3,
            Neurons = 4,
            Rules = 5,
            Accepts = 6,
            Incoming = 7,
            InputOffsets = 8,
            InputSteps = 9,
            States = 10,
            Emitting = 11,
            Trains = 12,
        }

        // The layouts below match the structs of the same names in Simulation.metal field for field.
        [StructLayout(LayoutKind.Sequential)]
        private struct Params
        {
            public uint SimCount;
            public int MaxSteps;
            public uint Steps;
            public uint LegacyTiming;
        }

        [StructLayout(LayoutKind.Sequential)]
        private record struct NetworkDesc
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
        private struct GpuSim
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
        private struct Progress
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
        private struct NeuronState
        {
            public long Spikes;
            public ushort LegacyDelay;
            public ushort ClosedFor;
            public ushort PendingEmission;
            public byte LegacyPending;
            public byte Pad;
        }
    }
}
