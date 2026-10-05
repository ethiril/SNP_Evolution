using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Compilation
{
    public enum Operation
    {
        // Adds one to the register, then goes to Next or Else, chosen at random when they differ.
        Add,

        // Takes one from the register and goes to Next, or goes to Else when the register is already zero.
        Sub,

        Halt,
    }

    public sealed record Instruction(Operation Operation, int Register = 0, int Next = 0, int Else = 0)
    {
        public override string ToString() => Operation switch
        {
            Operation.Add when Next == Else => $"ADD r{Register} -> {Next}",
            Operation.Add => $"ADD r{Register} -> {Next} | {Else}",
            Operation.Sub => $"SUB r{Register} -> {Next} else {Else}",
            _ => "HALT",
        };
    }

    // A register machine that generates numbers, as in the SN P universality proofs (Ionescu, Paun and Yokomori
    // 2006): it starts at instruction 0 with every register zero, and each computation that halts generates the
    // value of register 0. Register 0 is only ever added to, so it can be read out as the interval between two
    // spikes without a subtraction in the way.
    public sealed record RegisterProgram(int RegisterCount, IReadOnlyList<Instruction> Instructions)
    {
        public const int OutputRegister = 0;

        // Why the program cannot be compiled, or null when it can.
        public string? Problem()
        {
            if (Instructions.Count == 0)
            {
                return "it has no instructions";
            }
            for (int label = 0; label < Instructions.Count; label++)
            {
                Instruction instruction = Instructions[label];
                if (instruction.Operation == Operation.Halt)
                {
                    continue;
                }
                if (instruction.Register < 0 || instruction.Register >= RegisterCount)
                {
                    return $"instruction {label} uses r{instruction.Register}, but there are {RegisterCount} registers";
                }
                if (instruction.Operation == Operation.Sub && instruction.Register == OutputRegister)
                {
                    return $"instruction {label} subtracts from the output register r0";
                }
                if (new[] { instruction.Next, instruction.Else }.Any(target => target < 0 || target >= Instructions.Count))
                {
                    return $"instruction {label} goes to an instruction that does not exist";
                }
            }
            return null;
        }

        // Every number the program generates within the limits, following every choice. Complete is false when a
        // limit other than outputLimit cut the search short, so numbers may be missing. Steps is the most instructions any
        // computation that halted took. maxWork caps the configurations visited over all steps, so a program that
        // keeps growing its registers forever costs no more than that. A computation is dropped once register 0
        // passes outputLimit, which loses nothing at or below the limit since register 0 never goes down, or once
        // another register passes valueLimit, which a search can afford when it knows the largest number it wants.
        public (IReadOnlyList<int> Outputs, bool Complete, int Steps) Generate(int maxSteps = 20_000, int maxConfigurations = 5_000, long maxWork = 2_000_000,
            long outputLimit = long.MaxValue, long valueLimit = long.MaxValue)
        {
            var outputs = new SortedSet<int>();
            // A configuration is the label followed by the registers; two computations in the same configuration
            // generate the same numbers from there on, whatever step they are at.
            var seen = new HashSet<long[]>(ConfigurationComparer.Instance);
            var start = new long[RegisterCount + 1];
            var current = new List<long[]> { start };
            seen.Add(start);
            bool complete = true;
            int steps = 0;
            long work = 0;
            for (int step = 0; step < maxSteps && current.Count > 0; step++)
            {
                work += current.Count;
                if (work > maxWork)
                {
                    complete = false;
                    break;
                }
                var next = new List<long[]>();
                void Go(long[] configuration, int label)
                {
                    configuration[0] = label;
                    for (int register = 1; register < configuration.Length; register++)
                    {
                        if (register == OutputRegister + 1 ? configuration[register] > outputLimit : configuration[register] > valueLimit)
                        {
                            // Past outputLimit nothing is lost below it, so only valueLimit leaves the result incomplete.
                            complete &= register == OutputRegister + 1;
                            return;
                        }
                    }
                    if (seen.Add(configuration))
                    {
                        next.Add(configuration);
                    }
                }
                foreach (long[] configuration in current)
                {
                    Instruction instruction = Instructions[(int)configuration[0]];
                    int register = instruction.Register + 1;
                    switch (instruction.Operation)
                    {
                        case Operation.Halt:
                            long value = configuration[OutputRegister + 1];
                            if (value > 0 && value <= int.MaxValue)
                            {
                                outputs.Add((int)value);
                                steps = step;
                            }
                            break;
                        case Operation.Add:
                            long[] added = (long[])configuration.Clone();
                            added[register]++;
                            if (instruction.Else != instruction.Next)
                            {
                                Go((long[])added.Clone(), instruction.Else);
                            }
                            Go(added, instruction.Next);
                            break;
                        default:
                            long[] after = (long[])configuration.Clone();
                            if (after[register] > 0)
                            {
                                after[register]--;
                                Go(after, instruction.Next);
                            }
                            else
                            {
                                Go(after, instruction.Else);
                            }
                            break;
                    }
                }
                current = next;
                if (current.Count > maxConfigurations)
                {
                    current = current.Take(maxConfigurations).ToList();
                    complete = false;
                }
            }
            return (outputs.ToList(), complete && current.Count == 0, steps);
        }

        public override string ToString() =>
            string.Join(Environment.NewLine, Instructions.Select((instruction, label) => $"{label}: {instruction}")) + Environment.NewLine;

        // Reads one instruction per line, as ToString writes them; the "label:" prefix is optional.
        public static RegisterProgram Parse(string text)
        {
            var instructions = new List<Instruction>();
            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Split('#')[0].Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (line.IndexOf(':') is int colon && colon >= 0)
                {
                    line = line[(colon + 1)..].Trim();
                }
                string[] words = line.Replace("->", " ").Replace("|", " ").Replace("else", " ", StringComparison.OrdinalIgnoreCase)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                Operation operation = words[0].ToUpperInvariant() switch
                {
                    "ADD" => Operation.Add,
                    "SUB" => Operation.Sub,
                    "HALT" => Operation.Halt,
                    _ => throw new FormatException($"Unknown instruction '{words[0]}' in '{rawLine.Trim()}'."),
                };
                if (operation == Operation.Halt)
                {
                    instructions.Add(new Instruction(Operation.Halt));
                    continue;
                }
                if (words.Length < 3 || !words[1].StartsWith("r", StringComparison.OrdinalIgnoreCase))
                {
                    throw new FormatException($"Expected '{words[0]} rN -> L' in '{rawLine.Trim()}'.");
                }
                int register = int.Parse(words[1][1..]);
                int nextLabel = int.Parse(words[2]);
                int elseLabel = words.Length > 3 ? int.Parse(words[3]) : nextLabel;
                instructions.Add(new Instruction(operation, register, nextLabel, elseLabel));
            }
            int registers = instructions.Where(instruction => instruction.Operation != Operation.Halt).Select(instruction => instruction.Register + 1).DefaultIfEmpty(1).Max();
            return new RegisterProgram(registers, instructions);
        }

        private sealed class ConfigurationComparer : IEqualityComparer<long[]>
        {
            public static readonly ConfigurationComparer Instance = new ConfigurationComparer();

            public bool Equals(long[]? first, long[]? second) => first!.AsSpan().SequenceEqual(second);

            public int GetHashCode(long[] configuration)
            {
                var hash = new HashCode();
                foreach (long value in configuration)
                {
                    hash.Add(value);
                }
                return hash.ToHashCode();
            }
        }
    }
}
