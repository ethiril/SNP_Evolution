using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;

namespace SnpEvolution.Cli
{
    // evolve-parts: for each first-part contract the library folder has no part for, evolves one from scratch, verifies
    // it on the exhaustive engine, shrinks it on hardware cost, checks it past its cases and saves it. Each contract gets a seed of its own from
    // the run's seed, so the same seed writes the same library whichever contracts are run together. The summary reports every part's
    // robustness to jitter. RobustJitter, when above 0, makes the shrink keep the most robust part at that jitter rather than the smallest.
    internal static class PartsSession
    {
        public sealed record Options(int Seed, long Budget, IReadOnlyList<Contract> Contracts, string Folder, Func<ISimulationEngine> CreateEngine, bool Redo,
            string EngineOption = "", bool HardwareProfile = false, int RobustJitter = 0);

        // Robust holds the part's robustness at each of Robustness.Reported, and is null without a part.
        public sealed record Row(string Contract, string Status, long? Evaluations, LibraryPart? Part, IReadOnlyList<float>? Robust = null);

        // 0 when every contract asked for has a part, 2 when some are unsolved, 1 when the library folder cannot be used.
        public static int Run(Options options, Action<string> log)
        {
            ModuleLibrary library;
            try
            {
                library = PartLibraryFiles.Load(options.Folder, log);
            }
            catch (InvalidDataException exception)
            {
                log(exception.Message);
                return 1;
            }
            string run = $"evolve-parts --seed {options.Seed} --budget {options.Budget}{options.EngineOption}{(options.HardwareProfile ? " --profile hardware" : "")}{(options.RobustJitter > 0 ? $" --robust {options.RobustJitter}" : "")}";
            PartSearchSettings settings = SearchSettings(options.Budget, options.CreateEngine) with { HardwareProfile = options.HardwareProfile, RobustJitter = options.RobustJitter };
            var rows = new List<Row>();
            foreach (Contract contract in options.Contracts)
            {
                Module? kept = library.PartFor(contract.Name);
                if (kept != null && !options.Redo)
                {
                    rows.Add(new Row(contract.Name, "kept", null, kept.Part));
                    continue;
                }
                log($"Evolving a part for {contract.Name}.");
                PartOutcome outcome = PartEvolution.Evolve(contract, options.Seed, settings, log);
                if (outcome.Part is Part part && outcome.Measurement is PartMeasurement measurement)
                {
                    BoundedResult admission = BoundedCheck.Admit(part, log);
                    if (admission.Verdict is Verdict.Failed)
                    {
                        rows.Add(new Row(contract.Name, "fails past its cases", outcome.Evaluations, kept?.Part));
                        continue;
                    }
                    Module module = library.AddPart(measurement.ToLibraryPart(part, new PartOrigin(outcome.Seed, run, outcome.Evaluations)) with { Proven = admission.Proven }, run);
                    rows.Add(new Row(contract.Name, "solved", outcome.Evaluations, module.Part));
                }
                else
                {
                    rows.Add(new Row(contract.Name, "not solved", outcome.Evaluations, kept?.Part));
                }
            }
            IReadOnlyList<string> written = PartLibraryFiles.Save(library, options.Folder);
            log($"Saved {written.Count} part(s) to {options.Folder}.");
            rows = rows.Select(row => row with { Robust = row.Part == null ? null : Robustness.Reported.Select(jitter => Robustness.Of(row.Part.Part, jitter)).ToList() }).ToList();
            log(FormatTable(rows));
            log($"Robust j=N is the share of {Robustness.Runs} runs that keep the contract, latency aside, when each spike on each synapse may arrive up to N steps late.");
            return rows.All(row => row.Part != null) ? 0 : 2;
        }

        public const int Population = 60;

        // How evolve-parts searches for a part, which a run's proposed parts share.
        public static PartSearchSettings SearchSettings(long budget, Func<ISimulationEngine> createEngine) =>
            new PartSearchSettings(budget, budget / 4, Population, Catalog.ChoiceFor(Catalog.StructuralDefault), createEngine);

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
