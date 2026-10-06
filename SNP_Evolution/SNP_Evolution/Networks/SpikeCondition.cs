using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SnpEvolution.Networks
{
    // The spike counts a rule expression accepts. Expressions are regular expressions over the single letter 'a',
    // and such sets are always eventually periodic, so the set is stored as a lasso: a lookup table of TailLength
    // entries followed by a cycle of Period entries that repeats forever. Matching any count is then one lookup.
    // An empty neuron never matches, even when the expression accepts the empty word (a*, a?), since in an SN P
    // system a rule needs at least one spike to apply; otherwise an empty neuron fires every step, a free clock.
    public sealed class SpikeCondition
    {
        private const int MaxLassoLength = 1_000_000;

        private static readonly ConcurrentDictionary<string, SpikeCondition> Compiled = new ConcurrentDictionary<string, SpikeCondition>();

        private readonly bool[] accepts;

        private SpikeCondition(bool[] accepts, int tailLength)
        {
            this.accepts = accepts;
            TailLength = tailLength;
        }

        public int TailLength { get; }

        public int Period => accepts.Length - TailLength;

        // TailLength + Period entries; a GPU backend can upload this table unchanged.
        public ReadOnlySpan<bool> Accepts => accepts;

        // Throws ArgumentException for an expression that is not valid or uses unsupported regex syntax.
        public static SpikeCondition Parse(string expression) => Compiled.GetOrAdd(expression, Compile);

        public bool Matches(long spikes) => spikes >= 0 && accepts[IndexOf(spikes, TailLength, Period)];

        // A tail and one period past from settle it, since every count after that repeats one before.
        public long? SmallestAccepted(long from = 0)
        {
            for (long count = Math.Max(0, from); count <= Math.Max(from, TailLength) + Period; count++)
            {
                if (Matches(count))
                {
                    return count;
                }
            }
            return null;
        }

        // The entry of Accepts that answers for this many spikes; a flattened copy of the table looks up the same way.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(long spikes, int tailLength, int period) =>
            spikes < tailLength ? (int)spikes : tailLength + (int)((spikes - tailLength) % period);

        // Reading n spikes moves the automaton's set of active positions n times; the sets eventually repeat, which closes the lasso.
        private static SpikeCondition Compile(string expression)
        {
            var automaton = new PositionAutomaton(new ExpressionParser(expression).ParseWhole());
            // Count 0 always sits in the tail, as the cycle is looked for from count 1 on, so refusing it leaves the rest as is.
            var accepts = new List<bool> { false };
            var firstSeenAt = new Dictionary<string, int>();
            int[] active = automaton.Start;
            for (int count = 1; ; count++)
            {
                string key = string.Join(",", active);
                if (firstSeenAt.TryGetValue(key, out int cycleStart))
                {
                    return new SpikeCondition(accepts.ToArray(), cycleStart);
                }
                if (count > MaxLassoLength)
                {
                    throw new ArgumentException($"Rule expression '{expression}' is too complex to compile.");
                }
                firstSeenAt[key] = count;
                accepts.Add(automaton.Accepts(active));
                active = automaton.Next(active);
            }
        }
    }
}
