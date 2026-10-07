using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Application
{
    public enum TargetKind
    {
        // The numbers a generator can produce, in any order and each once: {1,2,3}.
        Set,

        // The gaps between successive output spikes, in order and with repeats: 1,1,2,3,5,8.
        Sequence,

        // The output spike train step by step, 1 for a spike: 0110.
        BinaryWord,
    }

    // An output to evolve a generator for, as the user typed it. Values are positive numbers for a set or sequence,
    // and 0s and 1s for a binary word.
    public sealed record OutputTarget(TargetKind Kind, IReadOnlyList<int> Values)
    {
        public static OutputTarget Set(IEnumerable<int> values) => new OutputTarget(TargetKind.Set, values.ToList());

        // Reads numbers separated by commas or spaces, ignoring brackets and a trailing "...", or a word of 0s and 1s.
        public static bool TryParse(TargetKind kind, string input, out OutputTarget target)
        {
            target = new OutputTarget(kind, Array.Empty<int>());
            string trimmed = input.Trim().TrimEnd('.', '…', ' ', ',').Trim('{', '}', '[', ']', '(', ')', ' ');
            string[] parts;
            if (kind == TargetKind.BinaryWord)
            {
                parts = trimmed.Where(character => !char.IsWhiteSpace(character) && character != ',' && character != '_')
                    .Select(character => character.ToString()).ToArray();
            }
            else
            {
                parts = trimmed.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            }
            var values = new List<int>();
            foreach (string part in parts)
            {
                bool valid = kind == TargetKind.BinaryWord
                    ? part is "0" or "1"
                    : int.TryParse(part, out int number) && number > 0;
                if (!valid)
                {
                    return false;
                }
                values.Add(int.Parse(part));
            }
            if (values.Count == 0 || (kind == TargetKind.BinaryWord && !values.Contains(1)))
            {
                return false;
            }
            target = new OutputTarget(kind, values);
            return true;
        }

        public ITask CreateTask(IFitnessFunction setFitness) => Kind switch
        {
            TargetKind.Set => new GeneratorTask("Generate " + this, Values.Distinct().ToList(), setFitness),
            TargetKind.Sequence => new SequenceTask("Spike intervals " + this, Values),
            _ => new SpikeWordTask("Spike train " + this, Values.Select(value => value == 1).ToList()),
        };

        public override string ToString() => Kind switch
        {
            TargetKind.Set => "{" + string.Join(",", Values) + "}",
            TargetKind.Sequence => string.Join(",", Values) + ",...",
            _ => string.Concat(Values),
        };
    }
}
