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
using static SnpEvolution.Cli.CommandLine;
using static SnpEvolution.Cli.CommandOptions;

namespace SnpEvolution.Cli
{
    // The commands that evolve, advise on or compile a network for a --target.
    internal static class TargetCommands
    {
        private const long PilotBudget = 500;

        // Evolves a network from scratch for the target, with the menu's default settings otherwise. A
        // sequence unless --kind says otherwise. With --advise on the advisor's suggestions are applied first, and
        // with --pilot on a quick pilot picks the algorithm. Exits with 2 when no network solved the target.
        internal static int Evolve(IReadOnlyDictionary<string, string> options)
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
            RunOutput.Save(geneticAlgorithm, EvolutionSession.NewOutputFolder(), "TargetNet", Console.WriteLine, evaluations);
            return EvolutionSession.IsSolved(geneticAlgorithm) ? 0 : 2;
        }

        // Compiles the target into a network that is correct by construction, then shrinks it for --shrink
        // generations (300 unless given; 0 skips shrinking). --generations and --lexicase (on unless given) are for
        // evolving a register program.
        internal static int Compile(IReadOnlyDictionary<string, string> options)
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

        internal static int Reach(IReadOnlyDictionary<string, string> options, string[] args)
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

        internal static int Advise(IReadOnlyDictionary<string, string> options)
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
                Console.Error.WriteLine("evolve, advise, compile and reach need a --target of positive numbers, or of 0s and 1s with --kind binary.");
                return null;
            }
            var settings = new Settings { Target = target, Task = Catalog.TargetTask };
            ApplyOptions(settings, options);
            return settings;
        }
    }
}
