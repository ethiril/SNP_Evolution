using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Model;

namespace SnpEvolution.Compilation
{
    // Builds a network whose output spike intervals are exactly the values of a recurrence, forever. Values are
    // held the textbook way, as twice as many spikes in a register neuron, and each interval is a stream of one
    // unit per step: the next value is made by draining, one after the other, the registers that hold the earlier
    // values it is the sum of, so the interval lasts exactly as long as the value. Every unit drained is also
    // written into fresh registers, which is how the new value is stored for the intervals that use it later.
    //
    // A register is a pair of neurons loaded alike: an emitter that sends one unit per step to the registers being
    // written, and a detector that stays silent until its last step, then starts the next register in the stream,
    // so the next one starts on the very next step. An idle register holds an even count and no rule applies; one
    // spike from a detector makes it odd, which starts it. The first values are plain timers that only count.
    //
    // Values are kept in Order + 1 slots that take turns, each with one register per use of a value in a later
    // interval, so the network has 1 + Initial.Count + 2 * Uses * (Order + 1) neurons.
    public static class RecurrenceCompiler
    {
        public static int NeuronCount(Recurrence recurrence) =>
            1 + recurrence.Initial.Count + 2 * recurrence.Uses * (recurrence.Order + 1);

        public static Network Compile(Recurrence recurrence)
        {
            int initialCount = recurrence.Initial.Count;
            int order = recurrence.Order;
            int slots = order + 1;
            // How far back each register in an interval's stream reaches, in stream order: c1 times 1, c2 times 2, ...
            List<int> back = recurrence.Coefficients.SelectMany((coefficient, index) => Enumerable.Repeat(index + 1, coefficient)).ToList();
            int uses = back.Count;
            const int output = 0;
            int Timer(int value) => value;  // value is 1-based
            int Emitter(int slot, int use) => 1 + initialCount + 2 * (Mod(slot, slots) * uses + use);
            int Detector(int slot, int use) => Emitter(slot, use) + 1;
            int[] Register(int slot, int use) => new[] { Emitter(slot, use), Detector(slot, use) };
            // The register that drains as use `use` of interval `interval` (1-based), in the slot of the value it holds.
            int[] Segment(int interval, int use) => Register(interval - back[use], use);

            var neurons = new Neuron?[NeuronCount(recurrence)];
            neurons[output] = new Neuron(new[] { Rule.Standard("a", 1, 1) }, 1, new int[0], isOutput: true);
            for (int value = 1; value <= initialCount; value++)
            {
                IEnumerable<int> next = value < initialCount ? new[] { Timer(value + 1) } : Segment(initialCount + 1, 0);
                long spikes = 2L * recurrence.Initial[value - 1] + (value == 1 ? 1 : 0);
                neurons[Timer(value)] = new Neuron(TimerRules(), spikes, Positions(next.Append(output)), isOutput: false);
            }
            for (int slot = 0; slot < slots; slot++)
            {
                for (int use = 0; use < uses; use++)
                {
                    // A register in this slot holds value m with m = slot (mod slots) and drains during interval m + back[use].
                    int written = slot + back[use];
                    IEnumerable<int> unitsTo = Enumerable.Range(0, uses).SelectMany(target => Register(written, target));
                    IEnumerable<int> startsNext = use < uses - 1 ? Segment(written, use + 1) : Segment(written + 1, 0).Append(output);
                    long preload = PreloadedValue(recurrence, slot, back[use]) is long value ? 2 * value : 0;
                    neurons[Emitter(slot, use)] = new Neuron(EmitterRules(), preload, Positions(unitsTo), isOutput: false);
                    neurons[Detector(slot, use)] = new Neuron(TimerRules(), preload, Positions(startsNext), isOutput: false);
                }
            }
            return new Network(neurons.Select(neuron => neuron!).ToList());
        }

        // The given value that a register in this slot must hold at the start, if any: one whose interval comes
        // after the given ones. Registers for values still to be made start empty.
        private static long? PreloadedValue(Recurrence recurrence, int slot, int back)
        {
            int initialCount = recurrence.Initial.Count;
            int slots = recurrence.Order + 1;
            for (int value = initialCount - recurrence.Order + 1; value <= initialCount; value++)
            {
                if (Mod(value, slots) == slot && value + back > initialCount)
                {
                    return recurrence.Initial[value - 1];
                }
            }
            return null;
        }

        // An odd count of 2v + 1 sends one unit (two spikes) per step for v steps, ending empty.
        private static IReadOnlyList<Rule> EmitterRules() => new[] { Rule.Standard("aaaaa(aa)*", 2, 2), Rule.Standard("aaa", 3, 2) };

        // An odd count of 2v + 1 stays silent for v - 1 steps and fires one spike on the v-th, ending empty.
        private static IReadOnlyList<Rule> TimerRules() => new[] { Rule.Forget("aaaaa(aa)*", 2), Rule.Standard("aaa", 3, 1) };

        private static IReadOnlyList<int> Positions(IEnumerable<int> indexes) => indexes.Select(index => index + 1).Distinct().OrderBy(position => position).ToList();

        private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;
    }
}
