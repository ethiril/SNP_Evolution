using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;

namespace SnpEvolution.Cli
{
    // evolve-parts: for each first-part contract the library folder has no part for, evolves one from scratch, verifies
    // it on the exhaustive engine, shrinks it on hardware cost and saves it. Each contract gets a seed of its own from
    // the run's seed, so the same seed writes the same library whichever contracts are run together.
    internal static class PartsSession
    {
        public sealed record Options(int Seed, long Budget, IReadOnlyList<Contract> Contracts, string Folder, Func<ISimulationEngine> CreateEngine, bool Redo,
            string EngineOption = "", bool HardwareProfile = false);

        public sealed record Row(string Contract, string Status, long? Evaluations, LibraryPart? Part);

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
            string run = $"evolve-parts --seed {options.Seed} --budget {options.Budget}{options.EngineOption}{(options.HardwareProfile ? " --profile hardware" : "")}";
            PartSearchSettings settings = SearchSettings(options.Budget, options.CreateEngine) with { HardwareProfile = options.HardwareProfile };
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
                    Module module = library.AddPart(LibraryPart.Of(part, measurement, new PartOrigin(outcome.Seed, run, outcome.Evaluations)), run);
                    rows.Add(new Row(contract.Name, "solved", outcome.Evaluations, module.Part));
                }
                else
                {
                    rows.Add(new Row(contract.Name, "not solved", outcome.Evaluations, kept?.Part));
                }
            }
            IReadOnlyList<string> written = PartLibraryFiles.Save(library, options.Folder);
            log($"Saved {written.Count} part(s) to {options.Folder}.");
            log(FormatTable(rows));
            return rows.All(row => row.Part != null) ? 0 : 2;
        }

        public const int Population = 60;

        // How evolve-parts searches for a part, which a run's proposed parts share.
        public static PartSearchSettings SearchSettings(long budget, Func<ISimulationEngine> createEngine) =>
            new PartSearchSettings(budget, budget / 4, Population, Catalog.ChoiceFor(Catalog.StructuralDefault), createEngine);

        // A part kept from an earlier run shows the evaluations that run spent on it.
        public static string FormatTable(IReadOnlyList<Row> rows)
        {
            var table = new List<string[]> { new[] { "Contract", "Result", "Evaluations", "Neurons", "Synapses", "Latency" } };
            table.AddRange(rows.Select(row => new[]
            {
                row.Contract,
                row.Status,
                (row.Evaluations ?? row.Part?.Origin.Evaluations)?.ToString(CultureInfo.InvariantCulture) ?? "-",
                row.Part?.Cost.Neurons.ToString(CultureInfo.InvariantCulture) ?? "-",
                row.Part?.Cost.Synapses.ToString(CultureInfo.InvariantCulture) ?? "-",
                row.Part?.Latency.ToString(CultureInfo.InvariantCulture) ?? "-",
            }));
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
