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

    // A stalled population is replaced by random programs, since one that overshoots the target is a dead end small edits rarely leave.
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

        // The description says which stage the best program was scored on.
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
                    return SolvedLastStage();
                }
                Breed();
                Progress progress = stall.Observe(parents[0].Fitness);
                if (progress == Progress.Improved)
                {
                    request.Log($"Program generation {generation} (stage {scoring.Stage + 1}/{scoring.Bounds.Count}): fitness {parents[0].Fitness:0.000}, " +
                        $"{parents[0].Program.Instructions.Count} instructions, outputs {Describe(parents[0].Outputs)}");
                }
                Restart(progress);
                return false;
            }

            bool SolvedLastStage()
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

            void Breed()
            {
                if (settings.Lexicase)
                {
                    // Lexicase lets a program alone in generating some number breed, so one that generates too much cannot take over.
                    Func<ScoredProgram> pick = LexicaseSelection.Picker(parents, random) ?? (() => parents[random.Next(parents.Count)]);
                    parents = Ranking.Rank(parents.Take(1).Concat(Enumerable.Range(0, settings.Population - 1).Select(_ => scoring.Score(edits.Mutate(pick().Program)))));
                    return;
                }
                IEnumerable<ScoredProgram> children = Enumerable.Range(0, settings.Population).Select(_ => scoring.Score(edits.Mutate(parents[random.Next(parents.Count)].Program)));
                parents = Ranking.Rank(children.Concat(parents)).Take(mu).ToList();
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

        // A program may only seem to solve a stage because it was cut short before going wrong.
        private static ScoredProgram? FailedConfirmation(ProgramScoring scoring, ScoredProgram program)
        {
            ScoredProgram rescored = scoring.Score(program.Program, ConfirmationScale);
            return Solved.Solves(rescored.Fitness) ? null : rescored;
        }

        private static string Describe(IReadOnlyList<int> outputs) =>
            "{" + string.Join(",", outputs.Take(12)) + (outputs.Count > 12 ? ",..." : "") + "}";
    }
}
