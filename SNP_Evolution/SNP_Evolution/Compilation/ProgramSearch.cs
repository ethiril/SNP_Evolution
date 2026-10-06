using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Tasks;

namespace SnpEvolution.Compilation
{
    public sealed record ProgramSearchSettings(int Generations = 2000, int Population = 100, int MaxInstructions = 16, int Registers = 4,
        int MaxConfigurations = 2_000, bool Lexicase = true);

    // Checks are what lexicase selection compares: one per target number in the stage, 1 when it is generated, and
    // last the share of the numbers generated that are in the target.
    public sealed record ScoredProgram(RegisterProgram Program, float Fitness, IReadOnlyList<int> Outputs, IReadOnlyList<float> Checks);

    // Evolves a register program that generates the target set. Programs are scored by running them, following
    // every choice, which takes far less time than simulating the network they compile to; the network is only
    // built for the winner. A (mu + lambda) search over instruction lists, shorter programs first among equals.
    //
    // The target is learned in stages, as iterative evolution learns a sequence: a stage looks only at numbers up to
    // a bound, from the smallest few target numbers up, and stops following a computation once its output register
    // passes the bound. Register 0 never goes down, so that loses nothing a stage looks at, and a stage costs only
    // as much as its bound. The last stage looks past the largest target number, so a program that overshoots it
    // is seen to.
    //
    // With Lexicase on, the whole population survives each generation apart from the best, and every parent is
    // picked by lexicase selection (Helmuth, Spector and Matheson 2015) over the checks: each pick goes through the
    // target numbers and the no-extras check in a random order, keeping only the programs best at each. A program
    // that generates a number nothing else does, or generates nothing extra, then breeds even with low fitness, so
    // a program that generates too much, such as every even number, cannot take over the population. When the best fitness has not improved for a while, every program but the best is replaced with a
    // random one, since a program that overshoots, such as one that generates every even number, is a dead end that
    // smaller edits rarely leave.
    public sealed class ProgramSearch
    {
        private const int FirstStageNumbers = 4;
        private const int NumbersPerStage = 2;
        private const int Patience = 100;
        private const int ConfirmationBudget = 20;

        private readonly IReadOnlyList<int> target;
        private readonly ProgramSearchSettings settings;
        private readonly Random random;

        public ProgramSearch(IEnumerable<int> target, ProgramSearchSettings settings, Random random)
        {
            this.target = target.Distinct().OrderBy(number => number).ToList();
            this.settings = settings;
            this.random = random;
            Bounds = StageBounds(this.target);
        }

        // The largest number each stage looks at; the last is beyond the target's largest.
        public IReadOnlyList<long> Bounds { get; }

        public int Stage { get; private set; }

        public int GenerationsRun { get; private set; }

        public static IReadOnlyList<long> StageBounds(IReadOnlyList<int> sortedTarget)
        {
            var bounds = new List<long>();
            for (int count = FirstStageNumbers; count < sortedTarget.Count; count += NumbersPerStage)
            {
                bounds.Add(sortedTarget[count - 1]);
            }
            bounds.Add(2L * sortedTarget[^1] + 2);
            return bounds;
        }

        // The best program at the last stage reached, scored on that stage.
        public ScoredProgram Run(Action<string> log, IEnumerable<RegisterProgram>? seeds = null)
        {
            int mu = settings.Lexicase ? settings.Population : Math.Max(1, settings.Population / 5);
            List<RegisterProgram> starting = (seeds ?? Array.Empty<RegisterProgram>()).Concat(Enumerable.Range(0, settings.Population).Select(_ => RandomProgram())).ToList();
            List<ScoredProgram> parents = Rank(starting.Select(program => Score(program))).Take(mu).ToList();
            float reported = -1;
            int stalled = 0;
            for (GenerationsRun = 0; GenerationsRun < settings.Generations; GenerationsRun++)
            {
                if (++stalled > Patience)
                {
                    stalled = 0;
                    parents = Rank(parents.Take(1).Concat(Enumerable.Range(0, mu - 1).Select(_ => Score(RandomProgram())))).ToList();
                }
                if (Solved.Solves(parents[0].Fitness) && !Confirm(parents))
                {
                    continue;
                }
                if (Solved.Solves(parents[0].Fitness))
                {
                    if (Stage == Bounds.Count - 1)
                    {
                        break;
                    }
                    Stage++;
                    reported = -1;
                    stalled = 0;
                    parents = Rank(parents.Select(parent => Score(parent.Program))).ToList();
                    log($"Program stage {Stage + 1}/{Bounds.Count}: numbers up to {Bounds[Stage]}.");
                    continue;
                }
                if (settings.Lexicase)
                {
                    List<ScoredProgram> pool = parents;
                    IEnumerable<ScoredProgram> picked = Enumerable.Range(0, settings.Population - 1).Select(_ => Score(Mutate(LexicasePick(pool).Program)));
                    parents = Rank(parents.Take(1).Concat(picked)).ToList();
                }
                else
                {
                    IEnumerable<ScoredProgram> children = Enumerable.Range(0, settings.Population).Select(_ => Score(Mutate(parents[random.Next(parents.Count)].Program)));
                    parents = Rank(children.Concat(parents)).Take(mu).ToList();
                }
                if (parents[0].Fitness > reported)
                {
                    stalled = 0;
                    reported = parents[0].Fitness;
                    log($"Program generation {GenerationsRun} (stage {Stage + 1}/{Bounds.Count}): fitness {reported:0.000}, " +
                        $"{parents[0].Program.Instructions.Count} instructions, outputs {Describe(parents[0].Outputs)}");
                }
            }
            return parents[0];
        }

        // Scored on the current stage: the Jaccard similarity of the numbers it generates up to the bound and the
        // target's. Limits scale with the bound, and a larger budget runs the program for longer.
        public ScoredProgram Score(RegisterProgram program) => Score(program, 1);

        private ScoredProgram Score(RegisterProgram program, int budget)
        {
            if (program.Problem() != null)
            {
                return new ScoredProgram(program, -1, Array.Empty<int>(), Array.Empty<float>());
            }
            long bound = Bounds[Stage];
            IReadOnlyList<int> outputs = program.Generate((int)Math.Min(int.MaxValue, budget * (20 * bound + 100)), budget * settings.MaxConfigurations,
                maxWork: budget * (60 * bound + 2_000), outputLimit: bound, valueLimit: budget * (4 * bound + 4)).Outputs;
            List<int> wanted = target.Where(number => number <= bound).ToList();
            var generated = outputs.ToHashSet();
            List<float> checks = wanted.Select(number => generated.Contains(number) ? 1f : 0f).ToList();
            checks.Add(outputs.Count == 0 ? 0 : (float)outputs.Count(wanted.Contains) / outputs.Count);
            return new ScoredProgram(program, new JaccardFitness(wanted).Score(outputs), outputs, checks);
        }

        private ScoredProgram LexicasePick(IReadOnlyList<ScoredProgram> pool)
        {
            List<ScoredProgram> candidates = pool.Where(program => program.Fitness >= 0).ToList();
            if (candidates.Count == 0)
            {
                return pool[random.Next(pool.Count)];
            }
            foreach (int check in Enumerable.Range(0, candidates[0].Checks.Count).OrderBy(_ => random.Next()))
            {
                if (candidates.Count == 1)
                {
                    break;
                }
                float best = candidates.Max(program => program.Checks[check]);
                candidates = candidates.Where(program => program.Checks[check] == best).ToList();
            }
            return candidates[random.Next(candidates.Count)];
        }

        // A program that seems to solve the stage may only have been cut short before it went wrong, so it is run
        // again with a much larger budget, and loses its place if it then does worse. True when it still solves it.
        private bool Confirm(List<ScoredProgram> parents)
        {
            ScoredProgram rescored = Score(parents[0].Program, ConfirmationBudget);
            if (Solved.Solves(rescored.Fitness))
            {
                return true;
            }
            parents[0] = rescored;
            parents.Sort((first, second) => second.Fitness.CompareTo(first.Fitness));
            return false;
        }

        private static IEnumerable<ScoredProgram> Rank(IEnumerable<ScoredProgram> programs) =>
            programs.OrderByDescending(program => program.Fitness).ThenBy(program => program.Program.Instructions.Count);

        private RegisterProgram RandomProgram()
        {
            int length = random.Next(2, Math.Max(3, settings.MaxInstructions / 2));
            var instructions = new List<Instruction>();
            for (int label = 0; label < length; label++)
            {
                instructions.Add(RandomInstruction(length, instructions));
            }
            return new RegisterProgram(settings.Registers, instructions);
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
                ? new Instruction(Operation.Add, random.Next(settings.Registers), next, otherwise)
                : new Instruction(Operation.Sub, 1 + random.Next(settings.Registers - 1), next, random.Next(length));
        }

        // One random edit, repeated now and then so a child can take a bigger step.
        private RegisterProgram Mutate(RegisterProgram parent)
        {
            RegisterProgram child = parent;
            do
            {
                child = Edit(child);
            }
            while (random.Next(3) == 0);
            return child;
        }

        private RegisterProgram Edit(RegisterProgram program)
        {
            List<Instruction> instructions = program.Instructions.ToList();
            int label = random.Next(instructions.Count);
            switch (random.Next(6))
            {
                case 0 when instructions.Count < settings.MaxInstructions:
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
                    instructions[label] = instructions[label] with { Register = random.Next(settings.Registers) };
                    break;
                case 4:
                    instructions[label] = instructions[label] with { Next = random.Next(instructions.Count) };
                    break;
                default:
                    instructions[label] = instructions[label] with { Else = random.Next(instructions.Count) };
                    break;
            }
            return new RegisterProgram(settings.Registers, instructions);
        }

        private static Instruction Shift(Instruction instruction, Func<int, int> move) =>
            instruction with { Next = move(instruction.Next), Else = move(instruction.Else) };

        private static string Describe(IReadOnlyList<int> outputs) =>
            "{" + string.Join(",", outputs.Take(12)) + (outputs.Count > 12 ? ",..." : "") + "}";
    }
}
