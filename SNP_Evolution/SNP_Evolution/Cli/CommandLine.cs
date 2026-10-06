using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;

namespace SnpEvolution.Cli
{
    // Non-interactive commands, so benchmarks can run from scripts:
    //   benchmark [--budget N] [--seeds N] [--population N] [--task NAME] [--algorithm NAME]
    //   select --task NAME [--budget N] [--seeds N] [--population N]
    //   evolve --target VALUES [--kind set|sequence|binary] [--generations N] [--population N] [--algorithm NAME] [--seed N]
    //          [--neurons N] [--iterative on|off] [--patience N] [--advise on] [--pilot on]
    //          [--lexicase on|off] [--modules on|off] [--freeze on|off] [--module-files a.json,b.json]
    //          [--triggered on|off] [--incubate N] [--evaluations N] [--library DIR] [--max-parts N] [--glue N] [--glue-weight X]
    //   advise --target VALUES [--kind ...] [any evolve option]: prints the suggested settings without evolving
    //   compile --target VALUES [--kind sequence|set] [--program FILE] [--generations N] [--lexicase on|off] [--shrink N] [--population N] [--seed N]:
    //          compiles a recurrence (sequence) or register program (set, evolved unless --program gives one), then shrinks it
    //   evolve-parts [--seed N] [--budget N] [--only NAME,NAME] [--library DIR] [--engine exact|sampled] [--redo on]:
    //          evolves, verifies, shrinks and saves a part for each first-part contract the library has no part for
    //   reach --target VALUES --evaluations N [--setups flat,modules,composition] [--seeds N] [--charge-parts on|off] [any evolve option]:
    //          runs each setup on seeds 1..N with the same budget and compares how far into the target they get
    //   tasks | algorithms
    // Benchmarks use the exhaustive engine unless given --engine sampled; --configurations N caps its search width.
    // Composition search builds from the part library in --library DIR, or the settings' part library folder.
    // NAME matches any task or algorithm whose name contains it, ignoring case.
    internal static class CommandLine
    {
        private const long PilotBudget = 500;
        private const string Usage =
            "Usage: snp-evolution [benchmark|select|tasks|algorithms] [--budget N] [--seeds N] [--population N] [--task NAME] [--algorithm NAME] [--engine exact|sampled] [--configurations N]\n" +
            "       snp-evolution evolve --target \"1,1,2,3,5,8,13\" [--kind set|sequence|binary] [--generations N] [--population N] [--algorithm NAME] [--seed N]\n" +
            "                    [--neurons N] [--iterative on|off] [--patience N] [--advise on] [--pilot on]\n" +
            "                    [--lexicase on|off] [--modules on|off] [--freeze on|off] [--module-files a.json,b.json]\n" +
            "                    [--triggered on|off] [--incubate N] [--evaluations N] [--library DIR] [--max-parts N] [--glue N] [--glue-weight X]\n" +
            "       snp-evolution advise --target \"1,1,2,3,5,8,13\" [same options as evolve]\n" +
            "       snp-evolution compile --target \"1,1,2,3,5,8,13\" [--kind sequence|set] [--program FILE] [--generations N] [--lexicase on|off] [--shrink N] [--seed N]\n" +
            "       snp-evolution reach --target \"1,1,2,3,5,8,13\" --evaluations N [--setups flat,modules,composition] [--seeds N] [--charge-parts on|off] [evolve options]\n" +
            "       snp-evolution evolve-parts [--seed N] [--budget N] [--only \"add,fan-out\"] [--library DIR] [--engine exact|sampled] [--redo on]";

        public static int Run(string[] args)
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 1; index + 1 < args.Length; index += 2)
            {
                options[args[index].TrimStart('-')] = args[index + 1];
            }
            var settings = new Settings();
            BenchmarkSettings benchmark = settings.BenchmarkSettings with
            {
                EvaluationBudget = Number(options, "budget", settings.EvaluationBudget),
                Seeds = (int)Number(options, "seeds", settings.BenchmarkSeeds),
                PopulationSize = (int)Number(options, "population", settings.BenchmarkPopulationSize),
                CreateEngine = Engine(options),
            };
            List<AlgorithmChoice> algorithms = Matching(AlgorithmCatalog.All, algorithm => algorithm.Name, options.GetValueOrDefault("algorithm"));
            if (args[0].ToLowerInvariant() is "benchmark" or "select" && algorithms.Any(algorithm => AlgorithmCatalog.IsComposition(algorithm.Name)))
            {
                string folder = options.GetValueOrDefault("library", settings.PartLibraryFolder);
                benchmark = benchmark with { Parts = PartLibraryFiles.Load(folder).Parts.Select(module => module.Part!).ToList() };
                Console.Error.WriteLine($"Composition search builds from {benchmark.Parts.Count} part(s) in {folder}.");
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

        // Evolves a network from scratch for the target, with the menu's default settings otherwise. A
        // sequence unless --kind says otherwise. With --advise on the advisor's suggestions are applied first, and
        // with --pilot on a quick pilot picks the algorithm. Exits with 2 when no network solved the target.
        private static int Evolve(IReadOnlyDictionary<string, string> options)
        {
            if (TargetSettings(options) is not Settings settings)
            {
                return 1;
            }
            if (IsOn(options, "advise"))
            {
                Advice advice = RunAdvisor.Advise(settings);
                RunAdvisor.Format(advice).ToList().ForEach(Console.WriteLine);
                RunAdvisor.ApplyAll(settings, advice);
                ApplyOptions(settings, options);
            }
            if (IsOn(options, "pilot"))
            {
                AlgorithmChoice winner = RunAdvisor.Pilot(settings, PilotBudget, Console.WriteLine);
                settings.Algorithm = Catalog.Algorithms.First(entry => entry.Name == winner.Name);
            }
            var random = options.ContainsKey("seed") ? new Random((int)Number(options, "seed", 0)) : new Random();
            BenchmarkTask task = settings.SelectedTask;
            Console.WriteLine("Evolving a network for {0} with {1}.", task.Name, settings.Algorithm.Name);
            foreach (string note in EvolutionSession.Notes(settings, task))
            {
                Console.WriteLine(note);
            }
            var evaluations = new EvaluationCounter();
            IGeneticAlgorithm geneticAlgorithm = EvolutionSession.Evolve(settings, task, factory => factory.NewNetwork(), random, Console.WriteLine, evaluations);
            EvolutionSession.Save(geneticAlgorithm, EvolutionSession.NewOutputFolder(), "TargetNet", Console.WriteLine, evaluations);
            return EvolutionSession.IsSolved(geneticAlgorithm) ? 0 : 2;
        }

        // Compiles the target into a network that is correct by construction, then shrinks it for --shrink
        // generations (300 unless given; 0 skips shrinking). --generations and --lexicase (on unless given) are for
        // evolving a register program.
        private static int Compile(IReadOnlyDictionary<string, string> options)
        {
            if (TargetSettings(options) is not Settings settings)
            {
                return 1;
            }
            var random = options.ContainsKey("seed") ? new Random((int)Number(options, "seed", 0)) : new Random();
            int shrink = options.TryGetValue("shrink", out string? value) && int.TryParse(value, out int generations) && generations >= 0 ? generations : 300;
            var compile = new CompileSession.Options(shrink, (int)Number(options, "generations", 2000), options.GetValueOrDefault("program"), Switch(options, "lexicase", true));
            return CompileSession.Run(settings, compile, random, Console.WriteLine);
        }

        // The seed is 1 and the budget the settings' part budget unless given; --only takes contract names, each matching any
        // contract whose name contains it, ignoring case. Exits with 2 when a contract is left without a part.
        private static int EvolveParts(IReadOnlyDictionary<string, string> options, Settings settings)
        {
            List<Contract> contracts = FirstParts.Contracts.ToList();
            if (options.GetValueOrDefault("only") is string only)
            {
                string[] names = only.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (names.FirstOrDefault(name => !contracts.Any(contract => contract.Name.Contains(name, StringComparison.OrdinalIgnoreCase))) is string unknown)
                {
                    Console.Error.WriteLine($"No first-part contract matches '{unknown}'. The contracts are: {string.Join(", ", contracts.Select(contract => contract.Name))}.");
                    return 1;
                }
                contracts = contracts.Where(contract => names.Any(name => contract.Name.Contains(name, StringComparison.OrdinalIgnoreCase))).ToList();
            }
            string engine = options.GetValueOrDefault("engine") is string named ? $" --engine {named}" : "";
            var parts = new PartsSession.Options(
                (int)Number(options, "seed", 1),
                Number(options, "budget", settings.PartBudget),
                contracts,
                options.GetValueOrDefault("library", settings.PartLibraryFolder),
                Engine(options),
                IsOn(options, "redo"),
                engine);
            return PartsSession.Run(parts, Console.WriteLine);
        }

        // Ten seeds unless given, every setup unless --setups names some, and the parts' cost charged to composition
        // search unless --charge-parts off.
        private static int Reach(IReadOnlyDictionary<string, string> options, string[] args)
        {
            if (TargetSettings(options) is not Settings settings)
            {
                return 1;
            }
            IReadOnlyList<ReachSession.Setup> setups = ReachSession.Setups;
            if (options.GetValueOrDefault("setups") is string named)
            {
                string[] names = named.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (names.FirstOrDefault(name => ReachSession.Setups.All(setup => setup.Name != name)) is string unknown)
                {
                    Console.Error.WriteLine($"No setup is called '{unknown}'. The setups are: {string.Join(", ", ReachSession.Setups.Select(setup => setup.Name))}.");
                    return 1;
                }
                setups = names.Select(name => ReachSession.Setups.Single(setup => setup.Name == name)).ToList();
            }
            string command = "dotnet run -- " + string.Join(" ", args.Select(arg => arg.Contains(' ') || arg.Contains(',') ? $"\"{arg}\"" : arg));
            return ReachSession.Run(settings, setups, (int)Number(options, "seeds", 10), Switch(options, "charge-parts", true), command, Console.WriteLine);
        }

        private static int Advise(IReadOnlyDictionary<string, string> options)
        {
            if (TargetSettings(options) is not Settings settings)
            {
                return 1;
            }
            RunAdvisor.Format(RunAdvisor.Advise(settings)).ToList().ForEach(Console.WriteLine);
            return 0;
        }

        // The menu's default settings with the target and any evolve options given; null after reporting a bad target.
        private static Settings? TargetSettings(IReadOnlyDictionary<string, string> options)
        {
            string kindName = options.GetValueOrDefault("kind", "sequence");
            TargetKind? kind = kindName.ToLowerInvariant() switch
            {
                "set" => TargetKind.Set,
                "sequence" => TargetKind.Sequence,
                "binary" => TargetKind.BinaryWord,
                _ => null,
            };
            if (kind == null || !options.TryGetValue("target", out string? values) || !OutputTarget.TryParse(kind.Value, values, out OutputTarget target))
            {
                Console.Error.WriteLine(Usage);
                Console.Error.WriteLine("evolve, advise and compile need a --target of positive numbers, or of 0s and 1s with --kind binary.");
                return null;
            }
            var settings = new Settings { Target = target, Task = Catalog.TargetTask };
            ApplyOptions(settings, options);
            return settings;
        }

        // The evolve options that were given, which also win over the advisor's suggestions.
        private static void ApplyOptions(Settings settings, IReadOnlyDictionary<string, string> options)
        {
            settings.MaxGenerations = (int)Number(options, "generations", settings.MaxGenerations);
            settings.PopulationSize = (int)Number(options, "population", settings.PopulationSize);
            settings.MaxNeurons = (int)Number(options, "neurons", settings.MaxNeurons);
            settings.StagnationPatience = (int)Number(options, "patience", settings.StagnationPatience);
            settings.IterativeEvolution = Switch(options, "iterative", settings.IterativeEvolution);
            settings.Lexicase = Switch(options, "lexicase", settings.Lexicase);
            settings.Modules = Switch(options, "modules", settings.Modules);
            settings.FreezeModules = Switch(options, "freeze", settings.FreezeModules);
            settings.TriggeredModules = Switch(options, "triggered", settings.TriggeredModules);
            settings.ModuleIncubation = (int)Number(options, "incubate", settings.ModuleIncubation);
            settings.MaxEvaluations = Number(options, "evaluations", settings.MaxEvaluations);
            settings.PartLibraryFolder = options.GetValueOrDefault("library", settings.PartLibraryFolder);
            settings.Composition = settings.Composition with
            {
                MaxParts = (int)Number(options, "max-parts", settings.Composition.MaxParts),
                MaxGlue = (int)Number(options, "glue", settings.Composition.MaxGlue),
                GlueEdits = options.TryGetValue("glue-weight", out string? weight) && double.TryParse(weight, NumberStyles.Float, CultureInfo.InvariantCulture, out double glue) && glue >= 0
                    ? glue
                    : settings.Composition.GlueEdits,
            };
            if (options.GetValueOrDefault("module-files") is string files)
            {
                settings.ModuleFiles = files.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                settings.Modules = true;
            }
            if (options.GetValueOrDefault("algorithm") is string algorithm)
            {
                settings.Algorithm = Catalog.Algorithms.FirstOrDefault(entry => entry.Name.Contains(algorithm, StringComparison.OrdinalIgnoreCase)) ?? settings.Algorithm;
            }
        }

        private static bool Switch(IReadOnlyDictionary<string, string> options, string name, bool fallback) =>
            options.ContainsKey(name) ? IsOn(options, name) : fallback;

        private static bool IsOn(IReadOnlyDictionary<string, string> options, string name) =>
            options.GetValueOrDefault(name) is string value && !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) && value != "0";

        private static Func<ISimulationEngine> Engine(IReadOnlyDictionary<string, string> options)
        {
            if (string.Equals(options.GetValueOrDefault("engine"), "sampled", StringComparison.OrdinalIgnoreCase))
            {
                return () => new SequentialCpuEngine();
            }
            int maxConfigurations = (int)Number(options, "configurations", ExhaustiveCpuEngine.DefaultMaxConfigurations);
            return () => new ExhaustiveCpuEngine(maxConfigurations);
        }

        private static long Number(IReadOnlyDictionary<string, string> options, string name, long fallback) =>
            options.TryGetValue(name, out string? value) && long.TryParse(value, out long number) && number > 0 ? number : fallback;

        private static List<T> Matching<T>(IEnumerable<T> items, Func<T, string> name, string? filter) =>
            items.Where(item => filter == null || name(item).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
