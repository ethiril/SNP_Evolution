using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Compilation;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Search
{
    // The last check is the share of generated numbers in the target, so lexicase rewards generating nothing extra.
    public sealed record ScoredProgram(RegisterProgram Program, float Fitness, IReadOnlyList<int> Outputs, IReadOnlyList<float> Checks) : IScored
    {
        public int Size => Program.Instructions.Count;
    }

    // A stage stops following a computation once register 0 passes its bound, which loses nothing since register 0 never goes down.
    public sealed class ProgramScoring
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

        // A larger scale runs the program for longer, for confirming a solve.
        public ScoredProgram Score(RegisterProgram program, int scale = 1)
        {
            budget.Charge(EvaluationKind.InterpreterRun, 1);
            if (program.Problem() != null)
            {
                return new ScoredProgram(program, -1, Array.Empty<int>(), new float[stageTask.ExpectedSet.Count + 1]);
            }
            long bound = Bounds[Stage];
            IReadOnlyList<int> outputs = program.Generate((int)Math.Min(int.MaxValue, scale * (20 * bound + 100)), scale * maxConfigurations,
                maxWork: scale * (60 * bound + 2_000), outputLimit: bound, valueLimit: scale * (4 * bound + 4)).Outputs;
            List<float> checks = stageTask.Checks(outputs).ToList();
            checks.Add(outputs.Count == 0 ? 0 : (float)outputs.Count(stageTask.ExpectedSet.Contains) / outputs.Count);
            return new ScoredProgram(program, stageTask.Score(outputs), outputs, checks);
        }

        private GeneratorTask StageTask()
        {
            List<int> wanted = target.Where(number => number <= Bounds[Stage]).ToList();
            return new GeneratorTask($"numbers up to {Bounds[Stage]}", wanted, new JaccardFitness(wanted));
        }
    }
}
