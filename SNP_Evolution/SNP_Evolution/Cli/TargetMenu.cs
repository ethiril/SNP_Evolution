using System;
using System.Collections.Generic;
using System.Linq;
using static SnpEvolution.Cli.MenuPrompts;

namespace SnpEvolution.Cli
{
    // Choosing the target a run evolves for, and how long it evolves for.
    internal static class TargetMenu
    {
        // Asks for the kind of target and then its values, and makes matching it the task; false when the user backs
        // out. Leaving the values empty keeps the current ones, if they are of the chosen kind.
        public static bool EditTarget(Settings settings)
        {
            TargetKind[] kinds = Enum.GetValues<TargetKind>();
            string[] labels =
            {
                "Set: the numbers a run can produce, e.g. {2,4,6,8}",
                "Sequence: gaps between output spikes, in order, e.g. 1,1,2,3,5,8,13",
                "Binary word: the spike train step by step, e.g. 0110100110010110",
            };
            IReadOnlyList<SavedRun> savedTargets = SavedRuns.Default.Targets();
            IReadOnlyList<string> options = savedTargets.Count > 0 ? labels.Append("Saved: a target from a saved run >").ToList() : labels;
            if (ConsoleUi.Choose(settings, "What kind of output should the system produce?", options, Array.IndexOf(kinds, settings.Target.Kind)) is not int choice)
            {
                return false;
            }
            if (choice == kinds.Length)
            {
                return ChooseSavedTarget(settings, savedTargets) || EditTarget(settings);
            }
            TargetKind kind = kinds[choice];
            bool accepted = false;
            string example = kind == TargetKind.BinaryWord ? "0110100110010110" : kind == TargetKind.Sequence ? "1,1,2,3,5,8,13" : "2,4,6,8";
            var notes = new List<string> { $"For example: {example}" };
            if (settings.Target.Kind == kind)
            {
                notes.Add($"Leave it empty to keep {settings.Target}.");
            }
            string invalid = kind == TargetKind.BinaryWord
                ? "Use only 0s and 1s, with at least one 1."
                : "Use positive whole numbers separated by commas or spaces.";
            ConsoleUi.PromptUntilAccepted($"Enter the {labels[choice].Split(':')[0].ToLowerInvariant()} to match", invalid, input =>
            {
                if (input.Trim().Length == 0 && settings.Target.Kind == kind)
                {
                    return accepted = true;
                }
                if (!OutputTarget.TryParse(kind, input, out OutputTarget target))
                {
                    return false;
                }
                settings.Target = target;
                return accepted = true;
            }, notes.ToArray());
            if (accepted)
            {
                settings.Task = Catalog.TargetTask;
            }
            return accepted;
        }

        // Makes matching a saved run's target the task; false when the user goes back.
        private static bool ChooseSavedTarget(Settings settings, IReadOnlyList<SavedRun> savedTargets)
        {
            if (ConsoleUi.Choose(settings, "Match the target of:",
                savedTargets.Select(run => $"{run.Settings.Target.Kind} {run.Settings.Target}   ({run.Name})").ToList()) is not int choice)
            {
                return false;
            }
            settings.Target = savedTargets[choice].Settings.Target;
            settings.Task = Catalog.TargetTask;
            return true;
        }

        // Asks how many generations to evolve a target for; false when the user backs out. Empty input means 1000.
        public static bool EditTargetGenerations(Settings settings)
        {
            const int DefaultGenerations = 1000;
            bool accepted = false;
            ConsoleUi.PromptUntilAccepted("Maximum number of generations", NotPositiveInteger, input =>
            {
                if (input.Trim().Length == 0)
                {
                    settings.MaxGenerations = DefaultGenerations;
                    return accepted = true;
                }
                if (!InputParsing.TryPositiveInt(input.Trim(), out int generations))
                {
                    return false;
                }
                settings.MaxGenerations = generations;
                return accepted = true;
            }, $"Target: {settings.Target.Kind} {settings.Target}", $"Leave it empty for {DefaultGenerations}. Evolution stops early once the target is matched.");
            return accepted;
        }
    }
}
