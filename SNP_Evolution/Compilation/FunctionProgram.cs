using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Compilation
{
    // One way a computation of a function program ended: the done port its HALT names, the output registers' values,
    // whether every other register was back at zero, and the latency the compiled part has on it.
    public sealed record FunctionOutcome(string Done, IReadOnlyDictionary<string, int> Outputs, bool Clean, int Steps, int Latency);

    // Every way a run on one input ended. Complete is false when some computation had not halted within the limits.
    public sealed record FunctionRun(IReadOnlyList<FunctionOutcome> Outcomes, bool Complete)
    {
        public int Steps => Outcomes.Select(outcome => outcome.Steps).DefaultIfEmpty(0).Max();
    }

    // A register program that computes a function of its inputs, as a part does: input i is loaded into register i when
    // the program starts, the outputs are the registers straight after the inputs, read when it halts, and each HALT
    // names a done port by number, so a program can branch to more than one. Registers past the outputs are scratch.
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

        // Runs the program on one input, following every choice an ADD offers, as a contract must hold on every computation.
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
            // A configuration met twice may be a loop that never halts, so the run is not complete; a choice whose two
            // paths meet again at the same step is taken for one too, which errs on the side of failing a program. A loop
            // that never halts takes more steps each time round, so it is caught by maxSteps instead.
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
                    long[] after = (long[])configuration.Clone();
                    int register = instruction.Register + 1;
                    if (instruction.Operation == Operation.Add)
                    {
                        after[register]++;
                        if (instruction.Else != instruction.Next)
                        {
                            revisited |= !Follow(next, visited, Go((long[])after.Clone(), instruction, tookElse: true));
                        }
                        revisited |= !Follow(next, visited, Go(after, instruction, tookElse: false));
                    }
                    else if (after[register] > 0)
                    {
                        after[register]--;
                        revisited |= !Follow(next, visited, Go(after, instruction, tookElse: false));
                    }
                    else
                    {
                        revisited |= !Follow(next, visited, Go(after, instruction, tookElse: true));
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
            IEnumerable<string> lines = text.Split('\n').Select(line =>
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
            });
            RegisterProgram parsed = RegisterProgram.Parse(string.Join("\n", lines));
            int registers = Math.Max(parsed.RegisterCount, inputs.Count + outputs.Count) + scratchRegisters;
            return new FunctionProgram(parsed with { RegisterCount = registers }, inputs, outputs, dones);
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

        private static long[] Go(long[] configuration, Instruction instruction, bool tookElse)
        {
            configuration[0] = tookElse ? instruction.Else : instruction.Next;
            configuration[^1] += RegisterMachineCompiler.Steps(instruction, tookElse);
            return configuration;
        }

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
