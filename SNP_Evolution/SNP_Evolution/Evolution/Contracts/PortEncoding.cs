using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Contracts
{
    public sealed record EncodedCase(InputSpikes Input, int StartStep);

    // Steps are input steps: a spike sent on step t reaches its neuron in time for step t + 1, as one a neuron fires on step t does.
    public static class PortEncoding
    {
        public static IReadOnlyList<int> Trigger(int step = 0) => new[] { step };

        public static IReadOnlyList<int> Interval(int n, int from = 0) => n >= 1
            ? new[] { from, from + n }
            : throw new ArgumentOutOfRangeException(nameof(n), n, "An interval is two spikes n steps apart, so n must be at least 1.");

        public static IReadOnlyList<int> Count(int n, int from = 0) => n >= 0
            ? Enumerable.Range(from, n).ToArray()
            : throw new ArgumentOutOfRangeException(nameof(n), n, "A count cannot be negative.");

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

        // Unary values load from step 0 and start follows the last of them, because a part wired after another also receives its data before its start.
        public static EncodedCase ForCase(Contract contract, ContractCase @case, int quietSteps = 0)
        {
            List<Port> inPorts = contract.DataIn.ToList();
            int lastUnary = inPorts
                .Where(port => port.Kind is PortKind.Interval or PortKind.Count)
                .SelectMany(port => Encode(port, @case.Inputs[port.Name], 0))
                .DefaultIfEmpty(-1)
                .Max();
            int start = Math.Max(quietSteps, lastUnary + 1);
            var steps = new List<IReadOnlyList<int>> { Trigger(start) };
            steps.AddRange(inPorts.Select(port => Encode(port, @case.Inputs[port.Name], port.Kind == PortKind.Binary ? start : 0)));
            return new EncodedCase(new InputSpikes(steps), start);
        }
    }
}
