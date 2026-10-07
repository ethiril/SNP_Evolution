using System;
using System.Linq;
using SnpEvolution.Compilation;
using SnpEvolution.Model;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Search
{
    // The compile route to a part (RESEARCH.md "Toward general synthesis", item 2): search for a register program that
    // computes the contract's cases in the interpreter, compile it with the textbook ADD and SUB modules and an output
    // drain, then verify the network on the exhaustive engine and shrink it as a part found by search is. The compiler is
    // only checked by construction for generators, so its parts are verified rather than trusted.
    public sealed class CompiledPartSearch : ISearch<MeasuredPart>
    {
        // Program search spends interpreter runs, which a network budget does not count, so it stops at a generation limit.
        public const int DefaultProgramGenerations = 2000;

        private readonly PartSearchSettings settings;
        private readonly int programGenerations;

        public CompiledPartSearch(PartSearchSettings? settings = null, int programGenerations = DefaultProgramGenerations)
        {
            this.settings = settings ?? PartSearchSettings.Default;
            this.programGenerations = programGenerations;
        }

        public string Name => "Compiled part search: find a register program, compile, verify and shrink a part";

        // Only a contract whose data ports are all counts has a register program, and one with none needs no program.
        public static bool Applies(Contract contract) => contract.Data.Any() && FunctionScoring.Fits(contract);

        public static PartOutcome Compile(Contract contract, int runSeed, PartSearchSettings settings, int programGenerations, EvaluationBudget budget, Action<string> log)
        {
            int seed = PartSearch.SeedFor(runSeed, contract.Name);
            SearchOutcome<MeasuredPart> outcome = new CompiledPartSearch(settings, programGenerations).Run(new SearchRequest<MeasuredPart>(new ContractTask(contract), budget, new Random(seed), log));
            return new PartOutcome(contract, seed, outcome.Spent, outcome.Best?.Part, outcome.Best?.Measurement, outcome.Best?.Compiled);
        }

        public SearchOutcome<MeasuredPart> Run(SearchRequest<MeasuredPart> request)
        {
            ContractTask task = request.Task is IContractTask contractTask ? ContractTask.Of(contractTask) : throw new ArgumentException("A part search needs a contract.", nameof(request));
            if (!Applies(task.Contract))
            {
                throw new ArgumentException($"Contract '{task.Contract.Name}' has no count ports, or a port that is not a count, so it cannot be compiled.", nameof(request));
            }
            string name = task.Contract.Name;
            SearchOutcome<RegisterProgram> found = new ProgramSearch().Run(new SearchRequest<RegisterProgram>(task, request.Budget, request.Random, line => { })
            {
                MaxGenerations = programGenerations,
                Cancellation = request.Cancellation,
            });
            long runs = request.Budget[EvaluationKind.InterpreterRun];
            if (!found.Solved || found.Best == null)
            {
                request.Log($"{name}: no program found in {found.Generations} generations ({runs} interpreter runs, best fitness {found.Fitness:0.000}, {found.Description}).");
                return new SearchOutcome<MeasuredPart>(found.Stop, null, 0, found.Description, request.Budget.Report());
            }
            RegisterProgram shortest = Shortest(found.Best, new FunctionScoring(task.Contract, new ProgramSearchSettings().MaxConfigurations, request.Budget), task);
            FunctionProgram program = FunctionScoring.Layout(task.Contract, shortest);
            Network compiled = RegisterMachineCompiler.Compile(program);
            HardwareCost cost = HardwareCost.Of(compiled);
            request.Log($"{name}: program found after {found.Generations} generations ({runs} interpreter runs), {found.Best.Instructions.Count} instructions, " +
                $"{shortest.Instructions.Count} once unneeded ones are taken out; compiled to {cost}.");
            // Program search spends no network evaluations, so the shrink gets the search's share as well, and the route
            // spends what the search route would.
            SearchOutcome<MeasuredPart> shrunk = new PartSearch(settings with { ShrinkBudget = settings.Budget + settings.ShrinkBudget }).ShrinkFrom(request, compiled);
            return shrunk.Best is MeasuredPart part ? shrunk with { Best = part with { Compiled = new CompiledFrom(program, cost) } } : shrunk;
        }

        // Takes out instructions one at a time while the program still computes every case, since each instruction left
        // in costs neurons the shrink may not find a way to remove.
        public static RegisterProgram Shortest(RegisterProgram program, FunctionScoring scoring, ITask task)
        {
            for (int label = program.Instructions.Count - 1; label >= 0 && program.Instructions.Count > 1; label--)
            {
                RegisterProgram shorter = ProgramEdits.Without(program, label);
                if (Solved.Solves(scoring.Score(shorter).Fitness, task))
                {
                    program = shorter;
                    label = Math.Min(label, program.Instructions.Count);
                }
            }
            return program;
        }
    }
}
