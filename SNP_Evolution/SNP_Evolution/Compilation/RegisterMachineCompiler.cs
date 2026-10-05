using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Networks;

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
            int output = builder.New(new[] { Fire("a", 1) }, isOutput: true);
            int[] registers = Enumerable.Range(0, program.RegisterCount)
                .Select(register => builder.New(register == RegisterProgram.OutputRegister ? CountdownRules() : RegisterRules()))
                .ToArray();
            int[] instructions = program.Instructions.Select((_, label) => builder.New(new[] { Fire("a", 1) }, initialSpikes: label == 0 ? 1 : 0)).ToArray();
            builder.Connect(registers[RegisterProgram.OutputRegister], output);
            for (int label = 0; label < program.Instructions.Count; label++)
            {
                Instruction instruction = program.Instructions[label];
                int self = instructions[label];
                switch (instruction.Operation)
                {
                    case Operation.Halt:
                        builder.Connect(self, output, registers[RegisterProgram.OutputRegister]);
                        break;
                    case Operation.Add:
                        builder.Connect(self, builder.New(new[] { Fire("a", 1, produce: 2) }, connections: new[] { registers[instruction.Register] }));
                        if (instruction.Next == instruction.Else)
                        {
                            builder.Connect(self, instructions[instruction.Next]);
                            break;
                        }
                        int whenNext = builder.New(GateRules(), connections: new[] { instructions[instruction.Next] });
                        int whenElse = builder.New(GateRules(), connections: new[] { instructions[instruction.Else] });
                        int choice = builder.New(new[] { Fire("a", 1), Fire("a", 1, delay: 1) }, connections: new[] { whenNext, whenElse });
                        int lateReference = builder.New(new[] { Fire("a", 1) }, connections: new[] { whenElse });
                        int reference = builder.New(new[] { Fire("a", 1) }, connections: new[] { whenNext, lateReference });
                        builder.Connect(self, choice, reference);
                        break;
                    default:
                        int aboveZero = builder.New(GateRules(), connections: new[] { instructions[instruction.Next] });
                        int atZero = builder.New(GateRules(), connections: new[] { instructions[instruction.Else] });
                        int early = builder.New(new[] { Fire("a", 1) }, connections: new[] { aboveZero });
                        int late = builder.New(new[] { Fire("a", 1, delay: 1) }, connections: new[] { atZero });
                        builder.Connect(self, registers[instruction.Register], early, late);
                        builder.Connect(registers[instruction.Register], aboveZero, atZero);
                        break;
                }
            }
            return builder.Build();
        }

        // Idle on an even count; one more spike asks whether it is above zero.
        private static IReadOnlyList<Rule> RegisterRules() => new[] { Fire("aaa(aa)*", 3), Fire("a", 1, delay: 1) };

        // On 2v + 1 spikes it is silent for v - 1 steps, then fires on the v-th; on one spike (v = 0) it forgets it.
        private static IReadOnlyList<Rule> CountdownRules() =>
            new[] { new Rule("aaaaa(aa)*", 0, false, 2), Fire("aaa", 3), new Rule("a", 0, false, 1) };

        // Fires when two spikes arrive together, and forgets one that arrives alone.
        private static IReadOnlyList<Rule> GateRules() => new[] { Fire("aa", 2), new Rule("a", 0, false, 1) };

        private static Rule Fire(string expression, long consume, int produce = 1, int delay = 0) => new Rule(expression, delay, true, consume, produce);

        private sealed class Builder
        {
            private readonly List<(IReadOnlyList<Rule> Rules, long Spikes, bool IsOutput)> neurons = new List<(IReadOnlyList<Rule>, long, bool)>();
            private readonly List<HashSet<int>> connections = new List<HashSet<int>>();

            public int New(IReadOnlyList<Rule> rules, long initialSpikes = 0, bool isOutput = false, IEnumerable<int>? connections = null)
            {
                neurons.Add((rules, initialSpikes, isOutput));
                this.connections.Add(new HashSet<int>(connections ?? Array.Empty<int>()));
                return neurons.Count - 1;
            }

            public void Connect(int from, params int[] to) => connections[from].UnionWith(to);

            public Network Build() => new Network(neurons
                .Select((neuron, index) => new Neuron(neuron.Rules, neuron.Spikes, connections[index].Select(target => target + 1).OrderBy(position => position).ToList(), neuron.IsOutput))
                .ToList());
        }
    }
}
