using System;
using System.Collections.Generic;
using System.Linq;
using static SnpEvolution.Simulation.Metal.ObjectiveC;

namespace SnpEvolution.Simulation.Metal
{
    // The kernel's buffers, one per slot, in memory the CPU and GPU share, so filling one is a plain write and nothing
    // is copied. A buffer grows as batches need and is kept between them.
    internal sealed unsafe class MetalBuffers
    {
        private readonly IntPtr device;
        private readonly Dictionary<int, SharedBuffer> buffers = new Dictionary<int, SharedBuffer>();

        public MetalBuffers(IntPtr device)
        {
            this.device = device;
        }

        // The kernel's buffer indexes.
        // The kernel's buffer indexes.
        public enum Slot
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

        // Every buffer by its slot, to bind for a dispatch.
        public IEnumerable<(int Slot, IntPtr Handle)> All => buffers.Select(pair => (pair.Key, pair.Value.Handle));

        // The buffer for the slot, grown to hold at least count items; its old contents are not kept.
        public Span<T> Buffer<T>(Slot slot, int count) where T : unmanaged
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

        public Span<T> View<T>(Slot slot, int count) where T : unmanaged =>
            new Span<T>((void*)Send(buffers[(int)slot].Handle, Selector("contents")), count);

        private sealed record SharedBuffer(IntPtr Handle, nuint Length);
    }
}
