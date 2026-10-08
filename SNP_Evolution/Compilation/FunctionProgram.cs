using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Compilation
{
    // Clean is whether every register but the outputs was back at zero; Latency is the compiled part's on this computation.
    public sealed record FunctionOutcome(string Done, IReadOnlyDictionary<string, int> Outputs, bool Clean, int Steps, int Latency);

    // Complete is false when some computation had not halted within the limits.
    public sealed record FunctionRun(IReadOnlyList<FunctionOutcome> Outcomes, bool Complete)
    {
        public int Steps => Outcomes.Select(outcome => outcome.Steps).DefaultIfEmpty(0).Max();
    }

    // Input i starts in register i, the outputs are the registers after the inputs, the rest are scratch, and each HALT names a done port by number.
    public sealed record FunctionProgram(RegisterProgram Program, IReadOnlyList<string> Inputs, IReadOnlyList<string> Outputs, IReadOnlyList<string> Dones)
    {
        public int RegistersNeeded => Inputs.Count + Outputs.Count;

        public int OutputRegister(int output) => Inputs.Count + output;

        public string? Problem()
        {
            if (Program.StructureProblem() is string problem)
            {
                return problem;
            }
            if (Program.RegisterCount < RegistersNeeded)
            {
                return $"it has {Program.RegisterCount} registers, but its inputs and outputs need {RegistersNeeded}";
            }
            int halt = Program.Instructions.ToList().FindIndex(instruction => instruction.Operation == Operation.Halt && (instruction.Register < 0 || instruction.Register >= Dones.Count));
            return halt >= 0 ? $"instruction {halt} halts on done port {Program.Instructions[halt].Register}, but there are {Dones.Count}" : null;
        }

        // Follows every choice an ADD offers, as a contract must hold on every computation.
        public FunctionRun Run(IReadOnlyDictionary<string, int> inputs, int maxSteps, int maxConfigurations = 2_000)
        {
            // A configuration is the label, the registers, then the steps the compiled network has taken so far.
            var start = new long[Program.RegisterCount + 2];
            for (int input = 0; input < Inputs.Count; input++)
            {
                start[input + 1] = inputs[Inputs[input]];
            }
            var outcomes = new List<FunctionOutcome>();
            var seen = new HashSet<string>();
            // A configuration carries its step count, so meeting one twice means two choices rejoined, which marks the run incomplete to err towards failing.
            bool revisited = false;
            var visited = new HashSet<long[]>(RegisterProgram.ConfigurationComparer.Instance) { start };
            var current = new List<long[]> { start };
            for (int step = 0; step < maxSteps && current.Count > 0; step++)
            {
                var next = new List<long[]>();
                foreach (long[] configuration in current)
                {
                    Instruction instruction = Program.Instructions[(int)configuration[0]];
                    if (instruction.Operation == Operation.Halt)
                    {
                        FunctionOutcome outcome = Outcome(configuration, instruction.Register, step);
                        if (seen.Add(Key(outcome)))
                        {
                            outcomes.Add(outcome);
                        }
                        continue;
                    }
                    foreach ((long[] after, bool tookElse) in RegisterProgram.Successors(instruction, configuration))
                    {
                        after[^1] += RegisterMachineCompiler.Steps(instruction, tookElse);
                        revisited |= !Follow(next, visited, after);
                    }
                }
                if (next.Count > maxConfigurations)
                {
                    return new FunctionRun(outcomes, false);
                }
                current = next;
            }
            return new FunctionRun(outcomes, current.Count == 0 && !revisited);
        }

        public override string ToString() =>
            $"inputs {Names(Inputs, 0)}; outputs {Names(Outputs, Inputs.Count)}; done {string.Join(", ", Dones.Select((done, index) => $"{index} = {done}"))}" + Environment.NewLine +
            string.Join(Environment.NewLine, Program.Instructions.Select((instruction, label) =>
                $"{label}: {(instruction.Operation == Operation.Halt ? $"HALT {Dones.ElementAtOrDefault(instruction.Register) ?? instruction.Register.ToString()}" : instruction.ToString())}")) + Environment.NewLine;

        // Reads instructions as RegisterProgram.Parse does, with a HALT naming its done port by name or number.
        public static FunctionProgram Parse(string text, IReadOnlyList<string> inputs, IReadOnlyList<string> outputs, IReadOnlyList<string> dones, int scratchRegisters = 0)
        {
            RegisterProgram parsed = RegisterProgram.Parse(string.Join("\n", text.Split('\n').Select(line => WithDoneNumber(line, dones))));
            int registers = Math.Max(parsed.RegisterCount, inputs.Count + outputs.Count) + scratchRegisters;
            return new FunctionProgram(parsed with { RegisterCount = registers }, inputs, outputs, dones);
        }

        private static string WithDoneNumber(string line, IReadOnlyList<string> dones)
        {
            string trimmed = line.Split('#')[0].Trim();
            string body = trimmed.Contains(':') ? trimmed[(trimmed.IndexOf(':') + 1)..].Trim() : trimmed;
            if (!body.StartsWith("HALT", StringComparison.OrdinalIgnoreCase))
            {
                return line;
            }
            string name = body[4..].Trim();
            int done = name.Length == 0 ? 0 : dones.ToList().IndexOf(name) is int index && index >= 0 ? index : int.Parse(name);
            return $"HALT {done}";
        }

        private FunctionOutcome Outcome(long[] configuration, int done, int step)
        {
            var outputs = Outputs.Select((name, output) => (name, value: (int)Math.Min(int.MaxValue, configuration[OutputRegister(output) + 1])))
                .ToDictionary(pair => pair.name, pair => pair.value);
            bool clean = Enumerable.Range(0, Program.RegisterCount)
                .Where(register => register < Inputs.Count || register >= RegistersNeeded)
                .All(register => configuration[register + 1] == 0);
            int latency = RegisterMachineCompiler.Latency((int)configuration[^1], outputs.Values);
            return new FunctionOutcome(Dones[done], outputs, clean, step, latency);
        }

        private static string Key(FunctionOutcome outcome) =>
            $"{outcome.Done}|{string.Join(",", outcome.Outputs.Select(pair => $"{pair.Key}={pair.Value}"))}|{outcome.Clean}|{outcome.Latency}";

        private static bool Follow(List<long[]> next, HashSet<long[]> visited, long[] configuration)
        {
            if (!visited.Add(configuration))
            {
                return false;
            }
            next.Add(configuration);
            return true;
        }

        private static string Names(IReadOnlyList<string> names, int first) =>
            names.Count == 0 ? "none" : string.Join(", ", names.Select((name, index) => $"r{first + index} = {name}"));
    }
}
