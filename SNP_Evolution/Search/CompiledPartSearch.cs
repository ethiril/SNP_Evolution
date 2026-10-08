using System;
using System.Linq;
using SnpEvolution.Compilation;
using SnpEvolution.Model;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Search
{
    // Finds a register program for the contract, compiles it, checks it past its cases (feeding counterexamples back), then
    // verifies and shrinks the network, since the compiler is only correct by construction for generators.
    public sealed class CompiledPartSearch : ISearch<MeasuredPart>
    {
        // Program search spends interpreter runs, which a network budget does not count, so it stops at a generation limit.
        public const int DefaultProgramGenerations = 2000;

        // A compiled part that fails past its cases adds the failing case to the program search's and searches again, after
        // CEGIS (Solar-Lezama et al. 2006), this many times in all; the last round's part is kept for admission to refuse.
        public const int CounterexampleRounds = 4;

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

        public static PartOutcome Compile(Contract contract, int runSeed, PartSearchSettings settings, int programGenerations, EvaluationBudget budget, Action<string> log) =>
            PartSearch.Find(new CompiledPartSearch(settings, programGenerations), contract, runSeed, budget, log);

        public SearchOutcome<MeasuredPart> Run(SearchRequest<MeasuredPart> request)
        {
            ContractTask task = PartSearch.TaskOf(request);
            if (!Applies(task.Contract))
            {
                throw new ArgumentException($"Contract '{task.Contract.Name}' has no count ports, or a port that is not a count, so it cannot be compiled.", nameof(request));
            }
            string name = task.Contract.Name;
            // The cases the program is searched on: the contract's, then each counterexample the admission check finds.
            ContractTask searched = task;
            RegisterProgram[] seeds = Array.Empty<RegisterProgram>();
            for (int round = 1; ; round++)
            {
                SearchOutcome<RegisterProgram> found = new ProgramSearch().Run(new SearchRequest<RegisterProgram>(searched, request.Budget, request.Random, line => { })
                {
                    Seeds = seeds,
                    MaxGenerations = programGenerations,
                    Cancellation = request.Cancellation,
                });
                long runs = request.Budget[EvaluationKind.InterpreterRun];
                if (!found.Solved || found.Best == null)
                {
                    request.Log($"{name}: no program found in {found.Generations} generations ({runs} interpreter runs, best fitness {found.Fitness:0.000}, {found.Description}).");
                    return new SearchOutcome<MeasuredPart>(found.Stop, null, 0, found.Description, request.Budget.Report());
                }
                RegisterProgram shortest = Shortest(found.Best, new FunctionScoring(searched.Contract, new ProgramSearchSettings().MaxConfigurations, request.Budget), searched);
                FunctionProgram program = FunctionScoring.Layout(task.Contract, shortest);
                Network compiled = RegisterMachineCompiler.Compile(program);
                HardwareCost cost = HardwareCost.Of(compiled);
                request.Log($"{name}: program found after {found.Generations} generations ({runs} interpreter runs), {found.Best.Instructions.Count} instructions, " +
                    $"{shortest.Instructions.Count} once unneeded ones are taken out; compiled to {cost}.");
                // Passing the cases is not enough: a program can overfit them, so the compiled part is checked past them first.
                if (BoundedCheck.Prove(new Part(task.Contract, compiled, task.Binding), BoundedCheck.Admission(task.Contract), request.Budget).Verdict is Verdict.Failed failed
                    && round < CounterexampleRounds)
                {
                    Counterexample counterexample = failed.Counterexample;
                    request.Log($"{name}: the compiled part fails at {counterexample.Inputs} ({ContractTask.RuleName(counterexample.Rule)}), so that case joins the search's cases.");
                    searched = WithCase(searched, counterexample);
                    seeds = new[] { shortest };
                    continue;
                }
                // Program search spends no network evaluations, so the shrink also gets the search's share and the route spends what the search route would.
                SearchOutcome<MeasuredPart> shrunk = new PartSearch(settings with { ShrinkBudget = settings.Budget + settings.ShrinkBudget }).ShrinkFrom(request, compiled);
                return shrunk.Best is MeasuredPart part ? shrunk with { Best = part with { Compiled = new CompiledFrom(program, cost) } } : shrunk;
            }
        }

        // The task with the counterexample's case added, allowed the latency the bounded check allowed it.
        public static ContractTask WithCase(ContractTask task, Counterexample counterexample) =>
            new ContractTask(task.Contract with
            {
                Cases = task.Contract.Cases.Append(counterexample.Case).ToList(),
                MaxLatency = Math.Max(task.Contract.MaxLatency, counterexample.Contract.MaxLatency),
            }, task.Binding);

        // Each instruction left in costs neurons the shrink may not find a way to remove.
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
