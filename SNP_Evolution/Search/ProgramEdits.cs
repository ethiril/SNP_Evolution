using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Compilation;

namespace SnpEvolution.Search
{
    public sealed class ProgramEdits
    {
        private readonly int maxInstructions;
        private readonly int registers;
        private readonly Random random;
        private readonly int dones;
        private readonly int firstSubtractable;

        private readonly bool choices;

        // A function program's ADDs never choose, since a contract holds on every computation and a choice can only fail it.
        public ProgramEdits(int maxInstructions, int registers, Random random, int dones = 1, int firstSubtractable = 1, bool choices = true)
        {
            this.maxInstructions = maxInstructions;
            this.registers = registers;
            this.random = random;
            this.dones = dones;
            this.firstSubtractable = firstSubtractable;
            this.choices = choices;
        }

        public static ProgramEdits ForFunctions(int maxInstructions, int registers, Random random, int dones) =>
            new ProgramEdits(maxInstructions, registers, random, dones, firstSubtractable: 0, choices: false);

        public RegisterProgram RandomProgram()
        {
            int length = random.Next(2, Math.Max(3, maxInstructions / 2));
            var instructions = new List<Instruction>();
            for (int label = 0; label < length; label++)
            {
                instructions.Add(RandomInstruction(length, instructions));
            }
            return new RegisterProgram(registers, instructions);
        }

        // One random edit, repeated now and then so a child can take a bigger step.
        public RegisterProgram Mutate(RegisterProgram parent)
        {
            RegisterProgram child = parent;
            do
            {
                child = Edit(child);
            }
            while (random.Next(3) == 0);
            return child;
        }

        // A choice often goes to a HALT already in the program, since stopping is how a computation generates a number.
        private Instruction RandomInstruction(int length, IReadOnlyList<Instruction> program)
        {
            int roll = random.Next(10);
            if (roll == 0)
            {
                // Drawing only when there is a choice keeps a generator search's random sequence as it was.
                return new Instruction(Operation.Halt, dones > 1 ? random.Next(dones) : 0);
            }
            List<int> halts = Enumerable.Range(0, Math.Min(length, program.Count)).Where(label => program[label].Operation == Operation.Halt).ToList();
            int next = random.Next(length);
            int otherwise = random.Next(3) != 0 ? next
                : halts.Count > 0 && random.Next(2) == 0 ? halts[random.Next(halts.Count)]
                : random.Next(length);
            return roll <= 5
                ? new Instruction(Operation.Add, random.Next(registers), next, choices ? otherwise : next)
                : new Instruction(Operation.Sub, firstSubtractable + random.Next(registers - firstSubtractable), next, random.Next(length));
        }

        private RegisterProgram Edit(RegisterProgram program)
        {
            List<Instruction> instructions = program.Instructions.ToList();
            int label = random.Next(instructions.Count);
            switch (random.Next(6))
            {
                case 0 when instructions.Count < maxInstructions:
                    // Insert before label; jumps to label and later move up by one so they still reach the same code.
                    instructions = instructions.Select(instruction => Shift(instruction, target => target >= label ? target + 1 : target)).ToList();
                    instructions.Insert(label, RandomInstruction(instructions.Count + 1, instructions));
                    break;
                case 1 when instructions.Count > 1:
                    return Without(program, label);
                case 2:
                    instructions[label] = RandomInstruction(instructions.Count, instructions);
                    break;
                case 3:
                    instructions[label] = instructions[label] with { Register = instructions[label].Operation == Operation.Halt ? random.Next(dones) : random.Next(registers) };
                    break;
                case 4:
                    instructions[label] = Jump(instructions[label], random.Next(instructions.Count), next: true);
                    break;
                default:
                    instructions[label] = Jump(instructions[label], random.Next(instructions.Count), next: false);
                    break;
            }
            return new RegisterProgram(registers, instructions);
        }

        // Without choices an ADD's two targets are one, so changing either moves both.
        private Instruction Jump(Instruction instruction, int target, bool next) =>
            !choices && instruction.Operation == Operation.Add ? instruction with { Next = target, Else = target }
                : next ? instruction with { Next = target } : instruction with { Else = target };

        // The program with one instruction taken out; a jump to it goes on to the instruction after it.
        public static RegisterProgram Without(RegisterProgram program, int label)
        {
            List<Instruction> instructions = program.Instructions.ToList();
            instructions.RemoveAt(label);
            return program with { Instructions = instructions.Select(instruction => Shift(instruction, target => Math.Min(instructions.Count - 1, target > label ? target - 1 : target))).ToList() };
        }

        private static Instruction Shift(Instruction instruction, Func<int, int> move) =>
            instruction with { Next = move(instruction.Next), Else = move(instruction.Else) };
    }
}
