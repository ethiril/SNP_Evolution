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

        public ProgramEdits(int maxInstructions, int registers, Random random)
        {
            this.maxInstructions = maxInstructions;
            this.registers = registers;
            this.random = random;
        }

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
                return new Instruction(Operation.Halt);
            }
            List<int> halts = Enumerable.Range(0, Math.Min(length, program.Count)).Where(label => program[label].Operation == Operation.Halt).ToList();
            int next = random.Next(length);
            int otherwise = random.Next(3) != 0 ? next
                : halts.Count > 0 && random.Next(2) == 0 ? halts[random.Next(halts.Count)]
                : random.Next(length);
            return roll <= 5
                ? new Instruction(Operation.Add, random.Next(registers), next, otherwise)
                : new Instruction(Operation.Sub, 1 + random.Next(registers - 1), next, random.Next(length));
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
                    instructions.RemoveAt(label);
                    instructions = instructions.Select(instruction => Shift(instruction, target => Math.Min(instructions.Count - 1, target > label ? target - 1 : target))).ToList();
                    break;
                case 2:
                    instructions[label] = RandomInstruction(instructions.Count, instructions);
                    break;
                case 3:
                    instructions[label] = instructions[label] with { Register = random.Next(registers) };
                    break;
                case 4:
                    instructions[label] = instructions[label] with { Next = random.Next(instructions.Count) };
                    break;
                default:
                    instructions[label] = instructions[label] with { Else = random.Next(instructions.Count) };
                    break;
            }
            return new RegisterProgram(registers, instructions);
        }

        private static Instruction Shift(Instruction instruction, Func<int, int> move) =>
            instruction with { Next = move(instruction.Next), Else = move(instruction.Else) };
    }
}
