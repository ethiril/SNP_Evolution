using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Compilation;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Tasks;

namespace SnpEvolution.Evolution.Search
{
    public sealed record ProgramSearchSettings(int Population = 100, int MaxInstructions = 16, int Registers = 4, int MaxConfigurations = 2_000, bool Lexicase = true);

    // Evolves a register program that generates the task's target set, scored stage by stage (see ProgramScoring); the
    // network is only built for the winner. A (mu + lambda) search over instruction lists, shorter programs first among
    // equals.
    //
    // With Lexicase on, the whole population survives each generation apart from the best, and every parent is picked
    // by lexicase selection over the checks, so a program that generates a number nothing else does, or generates
    // nothing extra, breeds even with low fitness, and a program that generates too much, such as every even number,
    // cannot take over the population. When the best fitness has stalled, every program but the best is replaced with
    // a random one, since a program that overshoots is a dead end that smaller edits rarely leave. A program that seems
    // to solve a stage may only have been cut short before it went wrong, so it is run again with a much larger budget
    // before the stage counts as solved.
    public sealed class ProgramSearch : ISearch<RegisterProgram>
    {
        private const int Patience = 100;
        private const int ConfirmationScale = 20;

        private readonly ProgramSearchSettings settings;

        public ProgramSearch(ProgramSearchSettings? settings = null)
        {
            this.settings = settings ?? new ProgramSearchSettings();
        }

        public string Name => "Register program search";

        // The best program at the last stage reached, scored on that stage, whose description says which stage that was.
        public SearchOutcome<RegisterProgram> Run(SearchRequest<RegisterProgram> request)
        {
            var task = request.Task as ISetTask ?? throw new ArgumentException("A program search needs a set of numbers to generate.", nameof(request));
            var scoring = new ProgramScoring(task.ExpectedSet, settings.MaxConfigurations, request.Budget);
            var edits = new ProgramEdits(settings.MaxInstructions, settings.Registers, request.Random);
            Random random = request.Random;
            int mu = settings.Lexicase ? settings.Population : Math.Max(1, settings.Population / 5);
            List<ScoredProgram> parents = Ranking.Rank(request.Seeds.Concat(Enumerable.Range(0, settings.Population).Select(_ => edits.RandomProgram())).Select(program => scoring.Score(program))).Take(mu).ToList();
            var stall = new StallDetector(Patience);

            bool Generation(int generation)
            {
                if (Solved.Solves(parents[0].Fitness))
                {
                    if (!SolveCheck.Confirms(parents[0].Fitness, () => FailedConfirmation(scoring, parents[0]), failed => parents = Ranking.Rank(parents.Skip(1).Prepend(failed))))
                    {
                        Restart(stall.Wait());
                        return false;
                    }
                    if (scoring.AtLastStage)
                    {
                        return true;
                    }
                    scoring.NextStage();
                    stall.Reset();
                    parents = Ranking.Rank(parents.Select(parent => scoring.Score(parent.Program)));
                    request.Log($"Program stage {scoring.Stage + 1}/{scoring.Bounds.Count}: numbers up to {scoring.Bounds[scoring.Stage]}.");
                    return false;
                }
                if (settings.Lexicase)
                {
                    Func<ScoredProgram> pick = LexicaseSelection.Picker(parents, random)!;
                    parents = Ranking.Rank(parents.Take(1).Concat(Enumerable.Range(0, settings.Population - 1).Select(_ => scoring.Score(edits.Mutate(pick().Program)))));
                }
                else
                {
                    IEnumerable<ScoredProgram> children = Enumerable.Range(0, settings.Population).Select(_ => scoring.Score(edits.Mutate(parents[random.Next(parents.Count)].Program)));
                    parents = Ranking.Rank(children.Concat(parents)).Take(mu).ToList();
                }
                Progress progress = stall.Observe(parents[0].Fitness);
                if (progress == Progress.Improved)
                {
                    request.Log($"Program generation {generation} (stage {scoring.Stage + 1}/{scoring.Bounds.Count}): fitness {parents[0].Fitness:0.000}, " +
                        $"{parents[0].Program.Instructions.Count} instructions, outputs {Describe(parents[0].Outputs)}");
                }
                Restart(progress);
                return false;
            }

            void Restart(Progress progress)
            {
                if (progress == Progress.Stalled)
                {
                    parents = Ranking.Rank(parents.Take(1).Concat(Enumerable.Range(0, mu - 1).Select(_ => scoring.Score(edits.RandomProgram()))));
                }
            }

            (SearchStop stop, int generations) = GenerationLoop.Run(request.MaxGenerations, () => request.Budget.IsSpent, request.Cancellation, Generation);
            ScoredProgram best = parents[0];
            string description = $"outputs {Describe(best.Outputs)} on numbers up to {scoring.Bounds[scoring.Stage]} (stage {scoring.Stage + 1}/{scoring.Bounds.Count})";
            return new SearchOutcome<RegisterProgram>(stop, best.Program, best.Fitness, description, request.Budget.Report()) { Generations = generations };
        }

        // The program run again for much longer, when it then no longer solves the stage.
        private static ScoredProgram? FailedConfirmation(ProgramScoring scoring, ScoredProgram program)
        {
            ScoredProgram rescored = scoring.Score(program.Program, ConfirmationScale);
            return Solved.Solves(rescored.Fitness) ? null : rescored;
        }

        private static string Describe(IReadOnlyList<int> outputs) =>
            "{" + string.Join(",", outputs.Take(12)) + (outputs.Count > 12 ? ",..." : "") + "}";
    }
}
