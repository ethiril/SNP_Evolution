using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Compilation;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Search
{
    // Summary says what the program did, for the search's log.
    public sealed record ScoredProgram(RegisterProgram Program, float Fitness, string Summary, IReadOnlyList<float> Checks) : IScored
    {
        public int Size => Program.Instructions.Count;
    }

    // How a program search scores a program, in stages that end with the whole target. A larger scale runs the program
    // for longer, for confirming a solve.
    public interface IProgramScoring
    {
        int Stage { get; }

        bool AtLastStage { get; }

        // The part of the target the current stage scores, such as "numbers up to 13 (stage 2/7)".
        string StageDescription { get; }

        void NextStage();

        ScoredProgram Score(RegisterProgram program, int scale = 1);
    }

    // A stage stops following a computation once register 0 passes its bound, which loses nothing since register 0 never
    // goes down. The last check is the share of generated numbers in the target, so lexicase rewards generating nothing extra.
    public sealed class ProgramScoring : IProgramScoring
    {
        private const int FirstStageNumbers = 4;
        private const int NumbersPerStage = 2;

        private readonly IReadOnlyList<int> target;
        private readonly int maxConfigurations;
        private readonly EvaluationBudget budget;
        private GeneratorTask stageTask;

        public ProgramScoring(IEnumerable<int> target, int maxConfigurations, EvaluationBudget budget)
        {
            this.target = target.Distinct().OrderBy(number => number).ToList();
            this.maxConfigurations = maxConfigurations;
            this.budget = budget;
            Bounds = StageBounds(this.target);
            stageTask = StageTask();
        }

        // The largest number each stage looks at; the last is beyond the target's largest.
        public IReadOnlyList<long> Bounds { get; }

        public int Stage { get; private set; }

        public bool AtLastStage => Stage == Bounds.Count - 1;

        public string StageDescription => $"numbers up to {Bounds[Stage]} (stage {Stage + 1}/{Bounds.Count})";

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

        public void NextStage()
        {
            Stage++;
            stageTask = StageTask();
        }

        public ScoredProgram Score(RegisterProgram program, int scale = 1)
        {
            budget.Charge(EvaluationKind.InterpreterRun, 1);
            if (program.Problem() != null)
            {
                return new ScoredProgram(program, -1, "not a program", new float[stageTask.ExpectedSet.Count + 1]);
            }
            long bound = Bounds[Stage];
            IReadOnlyList<int> outputs = program.Generate((int)Math.Min(int.MaxValue, scale * (20 * bound + 100)), scale * maxConfigurations,
                maxWork: scale * (60 * bound + 2_000), outputLimit: bound, valueLimit: scale * (4 * bound + 4)).Outputs;
            List<float> checks = stageTask.Checks(outputs).ToList();
            checks.Add(outputs.Count == 0 ? 0 : (float)outputs.Count(stageTask.ExpectedSet.Contains) / outputs.Count);
            return new ScoredProgram(program, stageTask.Score(outputs), "outputs " + Describe(outputs), checks);
        }

        private static string Describe(IReadOnlyList<int> outputs) =>
            "{" + string.Join(",", outputs.Take(12)) + (outputs.Count > 12 ? ",..." : "") + "}";

        private GeneratorTask StageTask()
        {
            List<int> wanted = target.Where(number => number <= Bounds[Stage]).ToList();
            return new GeneratorTask($"numbers up to {Bounds[Stage]}", wanted, new JaccardFitness(wanted));
        }
    }
}
