using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;
using SnpEvolution.Search.Benchmarking;

namespace SnpEvolution.Cli
{
    // The settings for a command that works on a --target, or a suite --task where it takes one: the starting settings,
    // what to evolve for, and the settings options given.
    internal static class TargetArgs
    {
        // Null, with the reason on stderr, when the target is not of its kind, the task names no one task, or both or
        // neither are given. A command that can run without either keeps its starting settings' task when needsOne is false.
        public static Settings? Settings(CommandArgs args, IEnumerable<SettingOption> options, bool needsOne = true)
        {
            Settings settings = args.StartingSettings();
            string? values = args.Find(CommonOptions.Target);
            string? name = args.Find(CommonOptions.Task);
            if (values != null && name != null)
            {
                return Refused($"Give {CommonOptions.Target.Flag} or {CommonOptions.Task.Flag}, not both.");
            }
            if (values != null)
            {
                TargetKind kind = args.Get(CommonOptions.Kind, TargetKind.Sequence);
                if (!OutputTarget.TryParse(kind, values, out OutputTarget target))
                {
                    return Refused($"{CommonOptions.Target.Flag} takes {CommonOptions.Target.Help}, not '{values}'.");
                }
                settings.Target = target;
                settings.UseTask(Catalog.TargetTask);
            }
            else if (name != null)
            {
                if (SuiteTask(name) is not CatalogEntry<Settings, BenchmarkTask> task)
                {
                    return Refused($"{CommonOptions.Task.Flag} needs to name one suite task, not '{name}'; run 'tasks' to list them.");
                }
                settings.UseTask(task);
            }
            else if (needsOne)
            {
                return Refused($"Give {CommonOptions.Target.Usage} or {CommonOptions.Task.Usage}.");
            }
            SettingOptions.Apply(settings, args, options);
            return settings;
        }

        // The one suite task the name picks, or null when it picks none or several.
        public static CatalogEntry<Settings, BenchmarkTask>? SuiteTask(string name) =>
            Catalog.Matching(Catalog.Tasks.Where(task => task != Catalog.TargetTask), task => task.Name, name) is { Count: 1 } matching ? matching[0] : null;

        // The target as --target takes it.
        public static string Text(OutputTarget target) => target.Kind == TargetKind.BinaryWord ? string.Concat(target.Values) : string.Join(",", target.Values);

        private static Settings? Refused(string reason)
        {
            System.Console.Error.WriteLine(reason);
            return null;
        }
    }
}
