using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SnpEvolution.Search;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Verification;
using SnpEvolution.Storage;

namespace SnpEvolution.Application
{
    // Both compiles and searches when no compiled part verifies; a part without count ports is always searched for.
    public enum PartRoute
    {
        Both,
        Search,
        Compile,
    }

    // Seed is the run's seed, which each contract's own seed comes from.
    public sealed record PartsRequest(IReadOnlyList<Contract> Contracts, int Seed = RunSeed.Repeatable, bool Redo = false, EngineChoice? Engine = null, int RobustJitter = 0, bool StagedCases = true,
        PartRoute Route = PartRoute.Both);

    // Error says why nothing was evolved, when the library folder cannot be used.
    public sealed record PartsResult(IReadOnlyList<PartsService.Row> Rows, string? Error = null)
    {
        public bool AllSolved => Error == null && Rows.All(row => row.Part != null);
    }

    // evolve-parts: for each first-part contract the library folder has no part for, evolves one from scratch, verifies
    // it on the exhaustive engine, shrinks it on hardware cost, checks it past its cases and saves it. Each contract gets a seed of its own from
    // the run's seed, so the same seed writes the same library whichever contracts are run together. The summary reports every part's
    // robustness to jitter. RobustJitter, when above 0, makes the shrink keep the most robust part at that jitter rather than the smallest.
    // The part budget, library folder and hardware profile come from the settings, for the menu and the command line alike.
    public static class PartsService
    {
        // Robust holds the part's robustness at each of Robustness.Reported, and is null without a part.
        public sealed record Row(string Contract, string Status, long? Evaluations, LibraryPart? Part, IReadOnlyList<float>? Robust = null);

        public static PartsResult Run(Settings settings, PartsRequest request, Action<string> log)
        {
            string folder = settings.PartLibraryFolder;
            long budget = settings.PartBudget;
            EngineChoice engine = request.Engine ?? new EngineChoice();
            Loaded<ModuleLibrary> loaded = PartLibraries.Load(folder, handBuilt: false, addLoop: false, log);
            if (loaded.Value is not ModuleLibrary library)
            {
                return new PartsResult(Array.Empty<Row>(), loaded.Error);
            }
            string run = $"evolve-parts --seed {request.Seed} --budget {budget}{engine.CommandLineFlag}{(settings.HardwareProfile ? " --profile hardware" : "")}{(request.RobustJitter > 0 ? $" --robust {request.RobustJitter}" : "")}{(request.StagedCases ? "" : " --staged off")}{(request.Route == PartRoute.Both ? "" : $" --route {request.Route.ToString().ToLowerInvariant()}")}";
            PartSearchSettings search = SearchSettings(budget, engine.Factory) with { HardwareProfile = settings.HardwareProfile, RobustJitter = request.RobustJitter, StagedCases = request.StagedCases };
            var rows = new List<Row>();
            foreach (Contract contract in request.Contracts)
            {
                if (EarlyStop.Requested)
                {
                    log("Stopped early: the contracts left are not evolved.");
                    break;
                }
                Module? kept = library.PartFor(contract.Name);
                if (kept != null && !request.Redo)
                {
                    rows.Add(new Row(contract.Name, "kept", null, kept.Part));
                    continue;
                }
                var spent = new EvaluationBudget();
                if (Find(contract, request, settings, search, spent, log) is not PartOutcome outcome)
                {
                    rows.Add(new Row(contract.Name, "not compilable", null, kept?.Part));
                    continue;
                }
                if (outcome.Part is Part part && outcome.Measurement is PartMeasurement measurement)
                {
                    BoundedResult admission = BoundedCheck.Admit(part, spent, log);
                    if (admission.Verdict is Verdict.Failed)
                    {
                        rows.Add(new Row(contract.Name, "fails past its cases", outcome.Evaluations, kept?.Part));
                        continue;
                    }
                    var origin = new PartOrigin(outcome.Seed, run, outcome.Evaluations, outcome.Compiled?.Program.ToString(), outcome.Compiled?.Compiled);
                    Module module = library.AddPart(measurement.ToLibraryPart(part, origin) with { Proven = admission.Proven }, run);
                    rows.Add(new Row(contract.Name, "solved", outcome.Evaluations, module.Part));
                }
                else
                {
                    rows.Add(new Row(contract.Name, EarlyStop.Requested ? "stopped early" : "not solved", outcome.Evaluations, kept?.Part));
                }
            }
            IReadOnlyList<string> written = PartLibraryFiles.Save(library, folder);
            log($"Saved {written.Count} part(s) to {folder}.");
            rows = rows.Select(row => row with { Robust = row.Part == null ? null : Robustness.Reported.Select(jitter => Robustness.Of(row.Part.Part, jitter, new EvaluationBudget())).ToList() }).ToList();
            log(FormatTable(rows));
            log($"Robust j=N is the share of {Robustness.Runs} runs that keep the contract, latency aside, when each spike on each synapse may arrive up to N steps late.");
            return new PartsResult(rows);
        }

        public const int Population = 60;

        // Null when only compiling was asked for and the contract cannot be; the compiler's rules are outside the hardware profile.
        private static PartOutcome? Find(Contract contract, PartsRequest request, Settings settings, PartSearchSettings search, EvaluationBudget spent, Action<string> log)
        {
            bool compiles = request.Route != PartRoute.Search && CompiledPartSearch.Applies(contract) && !settings.HardwareProfile;
            if (!compiles && request.Route == PartRoute.Compile)
            {
                log($"{contract.Name}: not compiled, since {(settings.HardwareProfile ? "the compiler's modules are outside the hardware profile" : "it has no count ports")}.");
                return null;
            }
            if (compiles)
            {
                log($"Compiling a part for {contract.Name}.");
                PartOutcome compiled = CompiledPartSearch.Compile(contract, request.Seed, search, CompiledPartSearch.DefaultProgramGenerations, spent, log);
                if (compiled.Solved || request.Route == PartRoute.Compile)
                {
                    return compiled;
                }
            }
            log($"Evolving a part for {contract.Name}.");
            return PartSearch.Evolve(contract, request.Seed, search, spent, log);
        }

        // How evolve-parts searches for a part, which a run's proposed parts share.
        public static PartSearchSettings SearchSettings(long budget, Func<ISimulationEngine> createEngine) =>
            new PartSearchSettings(budget, budget / 4, Population, Catalog.StructuralDefault, createEngine);

        // A part kept from an earlier run shows the evaluations that run spent on it.
        public static string FormatTable(IReadOnlyList<Row> rows)
        {
            var table = new List<string[]> { new[] { "Contract", "Result", "Evaluations", "Neurons", "Synapses", "Latency" }.Concat(Robustness.Reported.Select(jitter => $"Robust j={jitter}")).ToArray() };
            table.AddRange(rows.Select(row => new[]
            {
                row.Contract,
                row.Status,
                (row.Evaluations ?? row.Part?.Origin.Evaluations)?.ToString(CultureInfo.InvariantCulture) ?? "-",
                row.Part?.Cost.Neurons.ToString(CultureInfo.InvariantCulture) ?? "-",
                row.Part?.Cost.Synapses.ToString(CultureInfo.InvariantCulture) ?? "-",
                row.Part?.Latency.ToString(CultureInfo.InvariantCulture) ?? "-",
            }.Concat(Robustness.Reported.Select((_, index) => row.Robust?[index].ToString("0.00", CultureInfo.InvariantCulture) ?? "-")).ToArray()));
            int[] widths = Enumerable.Range(0, table[0].Length).Select(column => table.Max(cells => cells[column].Length)).ToArray();
            var text = new StringBuilder();
            foreach (string[] cells in table)
            {
                text.AppendLine(string.Join("   ", cells.Select((cell, column) => cell.PadRight(widths[column]))).TrimEnd());
            }
            return text.ToString();
        }
    }
}
