using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;
using static SnpEvolution.Cli.CommandOptions;
using static SnpEvolution.Cli.TargetCommands;
using static SnpEvolution.Cli.PartCommands;

namespace SnpEvolution.Cli
{
    // Non-interactive commands, so benchmarks can run from scripts:
    //   benchmark [--budget N] [--seeds N] [--population N] [--task NAME] [--algorithm NAME] [--lexicase on] [--library DIR] [--hand-built on|leaves]
    //   select --task NAME [--budget N] [--seeds N] [--population N]
    //   evolve --target VALUES [--kind set|sequence|binary] [--generations N] [--population N] [--algorithm NAME] [--seed N]
    //          [--neurons N] [--iterative on|off] [--patience N] [--advise on] [--pilot on]
    //          [--lexicase on|off] [--modules on|off] [--freeze on|off] [--module-files a.json,b.json]
    //          [--triggered on|off] [--incubate N] [--evaluations N] [--library DIR] [--max-parts N] [--glue N] [--glue-weight X]
    //   advise --target VALUES [--kind ...] [any evolve option]: prints the suggested settings without evolving
    //   compile --target VALUES [--kind sequence|set] [--program FILE] [--generations N] [--lexicase on|off] [--shrink N] [--population N] [--seed N]:
    //          compiles a recurrence (sequence) or register program (set, evolved unless --program gives one), then shrinks it
    //   evolve-parts [--seed N] [--budget N] [--only NAME,NAME] [--library DIR] [--engine exact|sampled] [--redo on] [--profile hardware]:
    //          evolves, verifies, shrinks and saves a part for each first-part contract the library has no part for
    //   reach --target VALUES --evaluations N [--setups flat,modules,composition] [--seeds N] [--charge-parts on|off] [any evolve option]:
    //          runs each setup on seeds 1..N with the same budget and compares how far into the target they get
    //   compose --task NAME [--library DIR] [--hand-built on|leaves] [--propose on|off] [--proposal-budget N] [--algorithm NAME] [--seed N]
    //          [--evaluations N] [--generations N] [--population N] [--max-parts N] [--glue N]:
    //          composition search for one suite task; a solved contract is promoted to a part and the library saved
    //   export-verilog --part FILE | --network FILE [--steps N] [--out DIR] [--check on|off]:
    //          writes a deterministic network as Verilog with a testbench, and co-simulates it under iverilog when installed
    //   export-nir --part FILE | --network FILE [--steps N] [--out DIR]:
    //          writes a hardware-profile network for tools/snp_nir.py, which makes the NIR file and co-simulates it in norse
    //   export-uppaal --part FILE [--out DIR] [--check on|off]:
    //          writes a part as Uppaal timed automata with queries for its contract, and model-checks them with verifyta when installed
    //   verify --part FILE | --library DIR [--only NAME,NAME] [--seconds N] [--bound N]:
    //          proves each part's contract for every input up to a bound, raised until --seconds per part run out, and records it in the part's file
    //   tasks | algorithms
    // evolve, compose and evolve-parts take --profile hardware, which keeps every rule to threshold-and-reset forms.
    // Benchmarks use the exhaustive engine unless given --engine sampled; --configurations N caps its search width.
    // Composition search builds from the part library in --library DIR, or the settings' part library folder.
    // NAME matches any task or algorithm whose name contains it, ignoring case, unless one name is exactly NAME.
    // --repetitions N sets how many sampled runs score each network in a benchmark.
    internal static class CommandLine
    {
        internal const string Usage =
            "Usage: snp-evolution [benchmark|select|tasks|algorithms] [--budget N] [--seeds N] [--population N] [--task NAME] [--algorithm NAME] [--engine exact|sampled] [--configurations N]\n" +
            "                    [--lexicase on] [--repetitions N] [--library DIR] [--hand-built on|leaves]\n" +
            "       snp-evolution evolve --target \"1,1,2,3,5,8,13\" [--kind set|sequence|binary] [--generations N] [--population N] [--algorithm NAME] [--seed N]\n" +
            "                    [--neurons N] [--iterative on|off] [--patience N] [--advise on] [--pilot on]\n" +
            "                    [--lexicase on|off] [--modules on|off] [--freeze on|off] [--module-files a.json,b.json]\n" +
            "                    [--triggered on|off] [--incubate N] [--evaluations N] [--library DIR] [--max-parts N] [--glue N] [--glue-weight X] [--profile hardware]\n" +
            "       snp-evolution advise --target \"1,1,2,3,5,8,13\" [same options as evolve]\n" +
            "       snp-evolution compile --target \"1,1,2,3,5,8,13\" [--kind sequence|set] [--program FILE] [--generations N] [--lexicase on|off] [--shrink N] [--seed N]\n" +
            "       snp-evolution reach --target \"1,1,2,3,5,8,13\" --evaluations N [--setups flat,modules,composition] [--seeds N] [--charge-parts on|off] [evolve options]\n" +
            "       snp-evolution evolve-parts [--seed N] [--budget N] [--only \"add,fan-out\"] [--library DIR] [--engine exact|sampled] [--redo on] [--profile hardware]\n" +
            "       snp-evolution compose --task \"Contract multiply\" [--library DIR] [--hand-built on] [--propose on|off] [--proposal-budget N] [--algorithm NAME] [--seed N] [--evaluations N]\n" +
            "       snp-evolution export-verilog --part parts/delay-2.json | --network FILE [--steps N] [--out DIR] [--check on|off]\n" +
            "       snp-evolution export-nir --part FILE | --network FILE [--steps N] [--out DIR]\n" +
            "       snp-evolution export-uppaal --part parts/delay-2.json [--out DIR] [--check on|off]\n" +
            "       snp-evolution verify --part parts/delay-2.json | --library DIR [--only \"add loop\"] [--seconds N] [--bound N]";

        public static int Run(string[] args)
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 1; index + 1 < args.Length; index += 2)
            {
                options[args[index].TrimStart('-')] = args[index + 1];
            }
            if (options.GetValueOrDefault("profile") is string profile && !profile.Equals("hardware", StringComparison.OrdinalIgnoreCase) && !profile.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"--profile takes hardware or none, not '{profile}'.");
                return 1;
            }
            var settings = new Settings();
            BenchmarkSettings benchmark = settings.BenchmarkSettings with
            {
                EvaluationBudget = Number(options, "budget", settings.EvaluationBudget),
                Seeds = (int)Number(options, "seeds", settings.BenchmarkSeeds),
                PopulationSize = (int)Number(options, "population", settings.BenchmarkPopulationSize),
                CreateEngine = Engine(options),
                Lexicase = Switch(options, "lexicase", false),
                Repetitions = (int)Number(options, "repetitions", settings.Repetitions),
            };
            List<AlgorithmChoice> algorithms = Matching(AlgorithmCatalog.All, algorithm => algorithm.Name, options.GetValueOrDefault("algorithm"));
            if (args[0].ToLowerInvariant() is "benchmark" or "select" && algorithms.Any(algorithm => AlgorithmCatalog.IsComposition(algorithm.Name)))
            {
                string folder = options.GetValueOrDefault("library", settings.PartLibraryFolder);
                ModuleLibrary library = PartLibrary(options, folder);
                benchmark = benchmark with { Parts = library.Parts.Select(module => module.Part!).ToList() };
                Console.Error.WriteLine($"Composition search builds from {benchmark.Parts.Count} part(s) in {folder}{(IsOn(options, "hand-built") ? " and the hand-built parts" : "")}.");
            }
            List<BenchmarkTask> tasks = Matching(TaskSuite.All, task => task.Name, options.GetValueOrDefault("task"));
            switch (args[0].ToLowerInvariant())
            {
                case "evolve":
                    return Evolve(options);
                case "advise":
                    return Advise(options);
                case "compile":
                    return Compile(options);
                case "evolve-parts":
                    return EvolveParts(options, settings);
                case "reach":
                    return Reach(options, args);
                case "compose":
                    return Compose(options);
                case "export-verilog":
                    return ExportCommands.Verilog(options);
                case "export-nir":
                    return ExportCommands.Nir(options);
                case "export-uppaal":
                    return ExportCommands.Uppaal(options);
                case "verify":
                    return VerifyCommand.Run(options, settings);
                case "tasks":
                    TaskSuite.All.ToList().ForEach(task => Console.WriteLine(task.Name));
                    return 0;
                case "algorithms":
                    AlgorithmCatalog.All.ToList().ForEach(algorithm => Console.WriteLine(algorithm.Name));
                    return 0;
                case "benchmark" when tasks.Count > 0 && algorithms.Count > 0:
                    Console.WriteLine(Benchmark.FormatTable(Benchmark.Run(algorithms, tasks, benchmark, Console.Error.WriteLine)));
                    return 0;
                case "select" when tasks.Count == 1 && algorithms.Count > 0:
                    SelectionResult result = AlgorithmSelector.Select(algorithms, tasks[0], benchmark, Math.Max(1, benchmark.EvaluationBudget / 8), Console.WriteLine);
                    Console.WriteLine("Best algorithm for {0}: {1}", tasks[0].Name, result.Winner.Name);
                    if (result.BestFound is Individual best)
                    {
                        Console.WriteLine("Best network found (fitness {0}, {1}):\n{2}", best.Fitness, best.Description, NetworkNotation.Format(best.Genes));
                    }
                    return 0;
                default:
                    Console.Error.WriteLine(Usage);
                    Console.Error.WriteLine("select needs a --task that matches exactly one task; run 'tasks' to list them.");
                    return 1;
            }
        }
    }
}
