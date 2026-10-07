using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;
using SnpEvolution.Evolution.Benchmarking;

namespace SnpEvolution.Cli
{
    // Exits with 2 when the task was not solved, so a script can tell a failed search from a bad command.
    internal sealed class ComposeCommand : Command
    {
        public override string Name => "compose";

        public override string Summary => "Composition search for one suite task; a solved contract is promoted to a part and the library saved.";

        public override IReadOnlyList<Option> Options { get; } =
            new Option[] { CommonOptions.Task, CommonOptions.Seed }.Concat(SettingOptions.Flags(SettingOptions.Evolve)).ToList();

        public override IReadOnlyList<Option> Required { get; } = new[] { CommonOptions.Task };

        public override ExitCode Run(CommandArgs args)
        {
            List<CatalogEntry<Settings, BenchmarkTask>> matching = Catalog.Matching(Catalog.Tasks.Skip(1), task => task.Name, args.Find(CommonOptions.Task));
            if (matching.Count != 1)
            {
                return Refuse("compose needs a --task that names one task; run 'tasks' to list them.");
            }
            Settings settings = ComposeService.SettingsFor(matching[0]);
            SettingOptions.Apply(settings, args, SettingOptions.Evolve);
            if (ComposeService.Problem(settings) is string problem)
            {
                return Refuse(problem);
            }
            BenchmarkTask task = settings.SelectedTask;
            Console.WriteLine("Composing a network for {0} with {1}.", task.Name, settings.Algorithm.Name);
            RunNotes.For(settings, task).ToList().ForEach(Console.WriteLine);
            EvolveResult result = ComposeService.Run(settings, RunSeed.For(CommonOptions.SeedFrom(args) ?? RunSeed.Repeatable), Console.WriteLine);
            return result.Error is string error ? Refuse(error) : result.Solved ? ExitCode.Success : ExitCode.Unsolved;
        }
    }
}
