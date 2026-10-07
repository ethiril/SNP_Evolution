using System.Collections.Generic;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // The settings for a command that works on a --target: the menu's defaults, the target, and the settings options given.
    internal static class TargetArgs
    {
        // Null, with the reason on stderr, when the target is not of its kind.
        public static Settings? Settings(CommandArgs args, IEnumerable<SettingOption> options)
        {
            TargetKind kind = args.Get(CommonOptions.Kind, "sequence") switch
            {
                "set" => TargetKind.Set,
                "binary" => TargetKind.BinaryWord,
                _ => TargetKind.Sequence,
            };
            string values = args.Get(CommonOptions.Target, "");
            if (!OutputTarget.TryParse(kind, values, out OutputTarget target))
            {
                System.Console.Error.WriteLine($"--target takes positive numbers, or 0s and 1s with --kind binary, not '{values}'.");
                return null;
            }
            var settings = new Settings { Target = target, Task = Catalog.TargetTask };
            SettingOptions.Apply(settings, args, options);
            return settings;
        }
    }
}
