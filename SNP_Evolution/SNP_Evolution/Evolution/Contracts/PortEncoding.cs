using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Contracts
{
    // A contract case as spikes for a part's input neurons, and the step the start spike is sent on.
    public sealed record EncodedCase(InputSpikes Input, int StartStep);

    // Turns port values into the steps their spikes are sent on. Every function is pure, since the exhaustive engine
    // needs the same input every time. Steps are input steps: a spike sent on step t reaches its neuron in time for
    // step t + 1, just as a spike a neuron fires on step t does.
    public static class PortEncoding
    {
        // The start spike, alone.
        public static IReadOnlyList<int> Start(int step = 0) => new[] { step };

        public static IReadOnlyList<int> Trigger(int step = 0) => new[] { step };

        // Two spikes n steps apart, the first on step from.
        public static IReadOnlyList<int> Interval(int n, int from = 0) => n >= 1
            ? new[] { from, from + n }
            : throw new ArgumentOutOfRangeException(nameof(n), n, "An interval is two spikes n steps apart, so n must be at least 1.");

        // n spikes, one per step, from step from; none for 0.
        public static IReadOnlyList<int> Count(int n, int from = 0) => n >= 0
            ? Enumerable.Range(from, n).ToArray()
            : throw new ArgumentOutOfRangeException(nameof(n), n, "A count cannot be negative.");

        // A spike on step from + i for each bit i of n that is 1, least significant first, for i below width.
        public static IReadOnlyList<int> Binary(int n, int width, int from = 0)
        {
            if (width < 1 || width > Port.MaxBinaryWidth)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, $"A binary word is 1 to {Port.MaxBinaryWidth} bits wide.");
            }
            if (n < 0 || n >= 1 << width)
            {
                throw new ArgumentOutOfRangeException(nameof(n), n, $"{n} does not fit in {width} bits.");
            }
            return Enumerable.Range(0, width).Where(bit => (n >> bit & 1) == 1).Select(bit => from + bit).ToArray();
        }

        public static IReadOnlyList<int> Encode(Port port, int value, int from) => port.Kind switch
        {
            PortKind.Interval => Interval(value, from),
            PortKind.Count => Count(value, from),
            PortKind.Trigger => value == 1 ? Trigger(from) : Array.Empty<int>(),
            _ => Binary(value, port.Width, from),
        };

        // The case's inputs in contract order: start first, then each data in-port, which is the order a part binds
        // its input neurons in. Unary values (interval and count) are loaded first, from step 0, and the start spike
        // follows on the step after the last of them, so a part is never started before its data has arrived; a part
        // wired after another receives its data the same way, since a part's data outputs come before its done.
        // Binary words stream in with start, bit i on step start + i. The start spike is never sent before
        // quietSteps, so a part that fires on its own is caught before it is started.
        public static EncodedCase ForCase(Contract contract, ContractCase @case, int quietSteps = 0)
        {
            List<Port> inPorts = contract.DataIn.ToList();
            int lastUnary = inPorts
                .Where(port => port.Kind is PortKind.Interval or PortKind.Count)
                .SelectMany(port => Encode(port, @case.Inputs[port.Name], 0))
                .DefaultIfEmpty(-1)
                .Max();
            int start = Math.Max(quietSteps, lastUnary + 1);
            var steps = new List<IReadOnlyList<int>> { Start(start) };
            steps.AddRange(inPorts.Select(port => Encode(port, @case.Inputs[port.Name], port.Kind == PortKind.Binary ? start : 0)));
            return new EncodedCase(new InputSpikes(steps), start);
        }
    }
}
