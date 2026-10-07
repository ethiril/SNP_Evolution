using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Model;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Search.Algorithms
{
    // A stage that has finished, or the one under way when the run stopped.
    public sealed record StageReport(int Length, int Generations, bool Solved, float BestFitness);

    // One stage: the task it scores on, and how many of the whole target's values or cases that task holds.
    public sealed record Stage(ITask Task, int Length);

    // Evolves for a long target a few values at a time: first for its opening values, and once a network reliably
    // gives those, for a few more. The same algorithm carries on from stage to stage, with everything it kept scored
    // again on the longer target, so the population, and an archive of stepping stones, survive each step. Each stage
    // only has to find a small change to networks that already work, and early stages are cheap because their runs
    // are short. Finished when the whole target is solved. The stages can be any tasks that grow towards the whole one,
    // such as a contract's cases a few at a time.
    public sealed class IterativeEvolution : IGeneticAlgorithm
    {
        private readonly IReadOnlyList<Stage> stages;
        private readonly int total;
        private readonly string unit;
        private readonly Func<ITask, FitnessEvaluator> createEvaluator;
        private readonly StageEvaluator evaluator;
        private readonly IGeneticAlgorithm algorithm;
        private readonly Action<string> log;
        private readonly List<StageReport> completedStages = new List<StageReport>();
        private int stageStartGeneration = 1;

        // createAlgorithm builds the algorithm once, scoring with the evaluator it is given, which follows the stages.
        public IterativeEvolution(
            IPrefixTask task,
            IReadOnlyList<int> lengths,
            Func<ITask, FitnessEvaluator> createEvaluator,
            Func<IPopulationEvaluator, IGeneticAlgorithm> createAlgorithm,
            Action<string>? log = null)
            : this(lengths.Select(length => new Stage(length >= task.Length ? task : task.Prefix(length), length)).ToList(), task.Length, "values", createEvaluator, createAlgorithm, log)
        {
        }

        // Unit names what the lengths count, for the log.
        public IterativeEvolution(
            IReadOnlyList<Stage> stages,
            int total,
            string unit,
            Func<ITask, FitnessEvaluator> createEvaluator,
            Func<IPopulationEvaluator, IGeneticAlgorithm> createAlgorithm,
            Action<string>? log = null)
        {
            if (stages.Count == 0)
            {
                throw new ArgumentException("There must be at least one stage.", nameof(stages));
            }
            this.stages = stages;
            this.total = total;
            this.unit = unit;
            this.createEvaluator = createEvaluator;
            this.log = log ?? Console.WriteLine;
            evaluator = new StageEvaluator(createEvaluator(stages[0].Task));
            algorithm = createAlgorithm(evaluator);
            this.log(StageAnnouncement());
        }

        public IReadOnlyList<Individual> Population => algorithm.Population;

        public int Generation => algorithm.Generation;

        // The best network for the stage under way, scored on that stage's target.
        public Individual? Best => algorithm.Best;

        public IReadOnlyList<IReadOnlyList<float>> FitnessHistory => algorithm.FitnessHistory;

        // Zero-based index of the stage under way.
        public int Stage { get; private set; }

        public int StageCount => stages.Count;

        public int StageLength => stages[Stage].Length;

        public bool IsComplete { get; private set; }

        public IGeneticAlgorithm Algorithm => algorithm;

        public IReadOnlyList<StageReport> Stages =>
            IsComplete ? completedStages : completedStages.Append(new StageReport(StageLength, Generation - stageStartGeneration, false, Best?.Fitness ?? 0)).ToList();

        public void NextGeneration()
        {
            if (IsComplete)
            {
                return;
            }
            algorithm.NextGeneration();
            if (algorithm.Best is not Individual best || !SolveCheck.Confirms(best, evaluator.Current))
            {
                return;
            }
            completedStages.Add(new StageReport(StageLength, Generation - stageStartGeneration, true, best.Fitness));
            log($"Stage {Stage + 1}/{StageCount} solved: the first {StageLength} of {total} {unit}, in {Generation - stageStartGeneration} generations.");
            if (Stage == stages.Count - 1)
            {
                IsComplete = true;
                return;
            }
            Stage++;
            stageStartGeneration = Generation;
            evaluator.Current = createEvaluator(stages[Stage].Task);
            algorithm.Rescore();
            log(StageAnnouncement());
        }

        public void Immigrate(IReadOnlyList<Network> newcomers) => algorithm.Immigrate(newcomers);

        public void Rescore() => algorithm.Rescore();

        private string StageAnnouncement() => $"Stage {Stage + 1}/{StageCount}: evolving for the first {StageLength} of {total} {unit}.";

        // Scores with whichever stage's evaluator is current.
        private sealed class StageEvaluator : ITaskEvaluator
        {
            public StageEvaluator(FitnessEvaluator current) => Current = current;

            public FitnessEvaluator Current { get; set; }

            public ITask Task => Current.Task;

            public IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks) => Current.EvaluateAll(networks);
        }
    }
}
