using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Model;

namespace SnpEvolution.Compilation
{
    // Compiles a register program into a network that generates the same numbers, read as the interval between the
    // output neuron's first two spikes. This follows the modules of the universality proofs (Ionescu, Paun and
    // Yokomori 2006) with standard rules E/a^c -> a^p;d: a register holding v holds 2v spikes, an instruction is a
    // neuron that fires when it gets one spike, and every module is checked by timing alone, so the network is
    // correct by construction and needs no search. It is much bigger than it has to be, which is what shrinking is for.
    //
    // ADD: the instruction sends one spike to a relay that adds two spikes to the register. A choice between two
    //   instructions goes through a neuron with two rules, one delayed by a step; two gates each see a reference
    //   spike and fire only when the choice arrives at the same step.
    // SUB: the instruction adds one spike to the register, making it odd. A register above zero takes three spikes
    //   and fires at once; at zero it takes the one and fires a step later. The instruction's own gates for "above
    //   zero" and "zero" get a reference spike on one of those two steps and fire when the register's spike matches.
    //   Gates of other instructions on the same register only ever see one spike, which they forget.
    // HALT: the instruction fires the output and starts register 0 counting down, one value per step; when it is
    //   done it fires the output again, so the interval is exactly its value.
    public static class RegisterMachineCompiler
    {
        public static Network Compile(RegisterProgram program)
        {
            if (program.Problem() is string problem)
            {
                throw new ArgumentException($"The program cannot be compiled: {problem}.");
            }
            var builder = new Builder();
            int output = builder.New(new[] { Rule.Standard("a", 1) }, isOutput: true);
            int[] registers = Enumerable.Range(0, program.RegisterCount)
                .Select(register => builder.New(register == RegisterProgram.OutputRegister ? CountdownRules() : RegisterRules()))
                .ToArray();
            int[] instructions = program.Instructions.Select((_, label) => builder.New(new[] { Rule.Standard("a", 1) }, initialSpikes: label == 0 ? 1 : 0)).ToArray();
            builder.Connect(registers[RegisterProgram.OutputRegister], output);
            Modules(builder, program, registers, instructions, (_, self) => builder.Connect(self, output, registers[RegisterProgram.OutputRegister]), _ => null);
            return builder.Build();
        }

        // Steps from an instruction neuron firing to the next one firing in a compiled network: an ADD passes straight on,
        // or through its choice, one step later for Else; a SUB's register answers above zero a step before it answers at zero.
        public static int Steps(Instruction instruction, bool tookElse) => instruction.Operation switch
        {
            Operation.Add when instruction.Next == instruction.Else => 1,
            Operation.Add => tookElse ? 4 : 3,
            _ => tookElse ? 4 : 3,
        };

        // A compiled function program's latency: a step from start to the first instruction, the instructions' own steps,
        // a SUB loop per output (3 steps a unit and 4 at zero), and a step from the HALT to its done port.
        public static int Latency(int instructionSteps, IEnumerable<int> outputs) => 1 + instructionSteps + outputs.Sum(value => 3 * value + 4) + 1;

        // Compiles a function program into a part's network, laid out start, the count in-ports, the count out-ports and the
        // done ports, then the rest. Start fires the first instruction. Each count in-port spike adds two to its register,
        // as the modules' registers hold 2v. When the program halts, each output register is drained by a SUB loop whose
        // "above zero" gate also fires the out-port, one spike per unit, and then the HALT fires its done port, so every
        // value is sent before done and every register ends at zero.
        public static Network Compile(FunctionProgram function)
        {
            if (function.Problem() is string problem)
            {
                throw new ArgumentException($"The program cannot be compiled: {problem}.");
            }
            (RegisterProgram program, IReadOnlyDictionary<int, int> drains) = WithDrains(function);
            var builder = new Builder();
            int start = builder.New(new[] { Rule.Standard("a", 1) }, isInput: true);
            int[] inputs = function.Inputs.Select(_ => builder.New(new[] { Rule.Standard("a", 1, produce: 2) }, isInput: true)).ToArray();
            int[] outputs = function.Outputs.Select(_ => builder.New(new[] { Rule.Standard("a", 1) })).ToArray();
            int[] dones = function.Dones.Select(_ => builder.New(new[] { Rule.Standard("a", 1) })).ToArray();
            int[] registers = Enumerable.Range(0, program.RegisterCount).Select(_ => builder.New(RegisterRules())).ToArray();
            int[] instructions = program.Instructions.Select(_ => builder.New(new[] { Rule.Standard("a", 1) })).ToArray();
            builder.Connect(start, instructions[0]);
            for (int input = 0; input < inputs.Length; input++)
            {
                builder.Connect(inputs[input], registers[input]);
            }
            Modules(builder, program, registers, instructions, (instruction, self) => builder.Connect(self, dones[instruction.Register]),
                label => drains.TryGetValue(label, out int output) ? outputs[output] : null);
            return builder.Build();
        }

        // The program with each HALT replaced by a SUB loop per output register, in order, ending on a HALT for the same
        // done port, and which loop labels drain which output.
        public static (RegisterProgram Program, IReadOnlyDictionary<int, int> Drains) WithDrains(FunctionProgram function)
        {
            List<Instruction> instructions = function.Program.Instructions.ToList();
            var drains = new Dictionary<int, int>();
            if (function.Outputs.Count == 0)
            {
                return (function.Program, drains);
            }
            for (int label = 0; label < function.Program.Instructions.Count; label++)
            {
                if (instructions[label] is not { Operation: Operation.Halt } halt)
                {
                    continue;
                }
                int at = label;
                for (int output = 0; output < function.Outputs.Count; output++)
                {
                    int after = instructions.Count;
                    instructions[at] = new Instruction(Operation.Sub, function.OutputRegister(output), at, after);
                    drains[at] = output;
                    instructions.Add(halt);
                    at = after;
                }
            }
            return (function.Program with { Instructions = instructions }, drains);
        }

        // The ADD and SUB modules for every instruction, with halt wiring each HALT and emit giving the neuron a SUB's
        // "above zero" gate also fires, if any.
        private static void Modules(Builder builder, RegisterProgram program, int[] registers, int[] instructions, Action<Instruction, int> halt, Func<int, int?> emit)
        {
            for (int label = 0; label < program.Instructions.Count; label++)
            {
                Instruction instruction = program.Instructions[label];
                int self = instructions[label];
                switch (instruction.Operation)
                {
                    case Operation.Halt:
                        halt(instruction, self);
                        break;
                    case Operation.Add:
                        builder.Connect(self, builder.New(new[] { Rule.Standard("a", 1, produce: 2) }, connections: new[] { registers[instruction.Register] }));
                        if (instruction.Next == instruction.Else)
                        {
                            builder.Connect(self, instructions[instruction.Next]);
                            break;
                        }
                        int whenNext = builder.New(GateRules(), connections: new[] { instructions[instruction.Next] });
                        int whenElse = builder.New(GateRules(), connections: new[] { instructions[instruction.Else] });
                        int choice = builder.New(new[] { Rule.Standard("a", 1), Rule.Standard("a", 1, delay: 1) }, connections: new[] { whenNext, whenElse });
                        int lateReference = builder.New(new[] { Rule.Standard("a", 1) }, connections: new[] { whenElse });
                        int reference = builder.New(new[] { Rule.Standard("a", 1) }, connections: new[] { whenNext, lateReference });
                        builder.Connect(self, choice, reference);
                        break;
                    default:
                        int aboveZero = builder.New(GateRules(), connections: new[] { instructions[instruction.Next] });
                        if (emit(label) is int port)
                        {
                            builder.Connect(aboveZero, port);
                        }
                        int atZero = builder.New(GateRules(), connections: new[] { instructions[instruction.Else] });
                        int early = builder.New(new[] { Rule.Standard("a", 1) }, connections: new[] { aboveZero });
                        int late = builder.New(new[] { Rule.Standard("a", 1, delay: 1) }, connections: new[] { atZero });
                        builder.Connect(self, registers[instruction.Register], early, late);
                        builder.Connect(registers[instruction.Register], aboveZero, atZero);
                        break;
                }
            }
        }

        // Idle on an even count; one more spike asks whether it is above zero.
        private static IReadOnlyList<Rule> RegisterRules() => new[] { Rule.Standard("aaa(aa)*", 3), Rule.Standard("a", 1, delay: 1) };

        // On 2v + 1 spikes it is silent for v - 1 steps, then fires on the v-th; on one spike (v = 0) it forgets it.
        private static IReadOnlyList<Rule> CountdownRules() =>
            new[] { Rule.Forget("aaaaa(aa)*", 2), Rule.Standard("aaa", 3), Rule.Forget("a", 1) };

        // Fires when two spikes arrive together, and forgets one that arrives alone.
        private static IReadOnlyList<Rule> GateRules() => new[] { Rule.Standard("aa", 2), Rule.Forget("a", 1) };

        private sealed class Builder
        {
            private readonly List<(IReadOnlyList<Rule> Rules, long Spikes, bool IsOutput, bool IsInput)> neurons = new List<(IReadOnlyList<Rule>, long, bool, bool)>();
            private readonly List<HashSet<int>> connections = new List<HashSet<int>>();

            public int New(IReadOnlyList<Rule> rules, long initialSpikes = 0, bool isOutput = false, IEnumerable<int>? connections = null, bool isInput = false)
            {
                neurons.Add((rules, initialSpikes, isOutput, isInput));
                this.connections.Add(new HashSet<int>(connections ?? Array.Empty<int>()));
                return neurons.Count - 1;
            }

            public void Connect(int from, params int[] to) => connections[from].UnionWith(to);

            public Network Build() => new Network(neurons
                .Select((neuron, index) => new Neuron(neuron.Rules, neuron.Spikes, connections[index].Select(target => target + 1).OrderBy(position => position).ToList(), neuron.IsOutput, isInput: neuron.IsInput))
                .ToList());
        }
    }
}
