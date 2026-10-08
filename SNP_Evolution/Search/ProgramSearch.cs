using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Compilation;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Operators;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Search
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
            (IProgramScoring scoring, ProgramEdits edits) = request.Task switch
            {
                ISetTask set => ((IProgramScoring)new ProgramScoring(set.ExpectedSet, settings.MaxConfigurations, request.Budget),
                    new ProgramEdits(settings.MaxInstructions, settings.Registers, request.Random)),
                IContractTask contract => Function(contract, request),
                _ => throw new ArgumentException("A program search needs a set of numbers to generate or a contract to compute.", nameof(request)),
            };
            Random random = request.Random;
            int mu = settings.Lexicase ? settings.Population : Math.Max(1, settings.Population / 5);
            List<ScoredProgram> parents = Ranking.Rank(request.Seeds.Concat(Enumerable.Range(0, settings.Population).Select(_ => edits.RandomProgram())).Select(program => scoring.Score(program))).Take(mu).ToList();
            var stall = new StallDetector(Patience);

            bool Generation(int generation)
            {
                if (Solved.Solves(parents[0].Fitness, request.Task))
                {
                    return SolvedLastStage();
                }
                Breed();
                Progress progress = stall.Observe(parents[0].Fitness);
                if (progress == Progress.Improved)
                {
                    request.Log($"Program generation {generation} on {scoring.StageDescription}: fitness {parents[0].Fitness:0.000}, " +
                        $"{parents[0].Program.Instructions.Count} instructions, {parents[0].Summary}");
                }
                Restart(progress);
                return false;
            }

            bool SolvedLastStage()
            {
                if (!SolveCheck.Confirms(parents[0].Fitness, () => FailedConfirmation(scoring, parents[0], request.Task), failed => parents = Ranking.Rank(parents.Skip(1).Prepend(failed))))
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
                request.Log($"Program stage: {scoring.StageDescription}.");
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
            string description = $"{best.Summary} on {scoring.StageDescription}";
            return new SearchOutcome<RegisterProgram>(stop, best.Program, best.Fitness, description, request.Budget.Report()) { Generations = generations };
        }

        // One scratch register at least, beyond the inputs and outputs.
        private (IProgramScoring, ProgramEdits) Function(IContractTask task, SearchRequest<RegisterProgram> request)
        {
            var scoring = new FunctionScoring(task.Contract, settings.MaxConfigurations, request.Budget);
            int registers = Math.Max(settings.Registers, scoring.RegistersNeeded + 1);
            return (scoring, ProgramEdits.ForFunctions(settings.MaxInstructions, registers, request.Random, task.Contract.Done.Count));
        }

        // A program may only seem to solve a stage because it was cut short before going wrong.
        private static ScoredProgram? FailedConfirmation(IProgramScoring scoring, ScoredProgram program, ITask task)
        {
            ScoredProgram rescored = scoring.Score(program.Program, ConfirmationScale);
            return Solved.Solves(rescored.Fitness, task) ? null : rescored;
        }
    }
}
