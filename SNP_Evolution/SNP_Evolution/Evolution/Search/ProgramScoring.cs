using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Compilation;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Tasks;

namespace SnpEvolution.Evolution.Search
{
    // Checks are what lexicase selection compares: one per target number in the stage, 1 when it is generated, and
    // last the share of the numbers generated that are in the target.
    public sealed record ScoredProgram(RegisterProgram Program, float Fitness, IReadOnlyList<int> Outputs, IReadOnlyList<float> Checks) : IScored
    {
        public int Size => Program.Instructions.Count;
    }

    // Scores register programs on a target set by running them, following every choice, which takes far less time than
    // simulating the network they compile to. The target is learned in stages, as iterative evolution learns a
    // sequence: a stage looks only at numbers up to a bound, from the smallest few target numbers up, and stops
    // following a computation once its output register passes the bound. Register 0 never goes down, so that loses
    // nothing a stage looks at, and a stage costs only as much as its bound. The last stage looks past the largest
    // target number, so a program that overshoots it is seen to. Each run is charged to the budget.
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

        // Scored on the current stage: the Jaccard similarity of the numbers it generates up to the bound and the
        // target's. Limits scale with the bound, and a larger scale runs the program for longer.
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
