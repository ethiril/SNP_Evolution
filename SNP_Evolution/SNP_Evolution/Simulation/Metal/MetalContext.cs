using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static SnpEvolution.Simulation.Metal.ObjectiveC;

namespace SnpEvolution.Simulation.Metal
{
    // One sampled computation for the GPU to run: a trial, which repetition of it this is, and that run's seed.
    internal readonly record struct GpuRun(Trial Trial, uint Seed);

    // What one run ended with. SpikeSteps is empty unless the trial reads a spike train.
    internal readonly record struct GpuRunResult(int? Output, bool Halted, int[] SpikeSteps);

    // The Metal device and the compiled kernel, shared by the whole process, and the dispatches that run a batch laid
    // out in its buffers (see GpuBatch) a chunk of steps at a time.
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
        private readonly MetalBuffers buffers;
        // Measured from the last dispatch and used to size the next, so each command buffer lasts about the target.
        private double secondsPerNeuronStep = 1e-9;

        private MetalContext(IntPtr device, IntPtr queue, IntPtr pipeline)
        {
            this.device = device;
            this.queue = queue;
            this.pipeline = pipeline;
            maxThreadsPerGroup = (int)SendForUnsigned(pipeline, Selector("maxTotalThreadsPerThreadgroup"));
            buffers = new MetalBuffers(device);
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
                GpuBatch batch = GpuBatch.Fill(buffers, runs, options, maxThreadsPerGroup);
                Execute(runs.Count, batch, options);
                return batch.Collect(buffers, runs);
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

        // Runs the kernel a chunk of steps at a time until every run has finished.
        private void Execute(int runCount, GpuBatch batch, SimulationOptions options)
        {
            Span<Progress> progress = buffers.View<Progress>(MetalBuffers.Slot.Progress, runCount);
            bool finished = false;
            while (!finished)
            {
                // A run can take one step beyond MaxSteps, to settle whether it halted.
                double affordable = TargetDispatchSeconds / (secondsPerNeuronStep * batch.NeuronCount);
                int steps = (int)Math.Min(Math.Max(affordable, 8), options.MaxSteps + 1);
                double seconds = Dispatch(runCount, batch.ThreadsPerGroup, steps, options);
                finished = true;
                foreach (Progress run in progress)
                {
                    finished &= run.Finished != 0;
                }
                if (!finished && seconds > 0)
                {
                    secondsPerNeuronStep = seconds / ((double)steps * batch.NeuronCount);
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
                foreach ((int slot, IntPtr handle) in buffers.All)
                {
                    Send(encoder, Selector("setBuffer:offset:atIndex:"), handle, 0, (nuint)slot);
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

        // The layout below matches the struct of the same name in Simulation.metal field for field.
        [StructLayout(LayoutKind.Sequential)]
        private struct Params
        {
            public uint SimCount;
            public int MaxSteps;
            public uint Steps;
            public uint LegacyTiming;
        }
    }
}
