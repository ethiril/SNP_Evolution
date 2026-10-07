using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // A command's form: Run, then the command's own options, then a page for each group of settings it takes. Changes
    // made here are for this command's runs only; the settings menu changes them for every run.
    internal static class CommandScreen
    {
        public static void Show(MenuState state, CommandItem item)
        {
            if (item.Command.Options.Count == 0)
            {
                Run(state, item, new Dictionary<Option, string>());
                return;
            }
            if (!state.Typed.TryGetValue(item, out Dictionary<Option, string>? typed))
            {
                state.Typed[item] = typed = new Dictionary<Option, string>();
            }
            List<Option> own = item.Command.Options.Where(option => SettingOptions.For(option) == null).ToList();
            List<(string Name, List<Option> Options)> groups = SettingsMenu.Groups
                .Select(group => (group.Name, group.Options.Select(setting => setting.Option).Where(item.Command.Options.Contains).ToList()))
                .Where(group => group.Item2.Count > 0)
                .ToList();
            int selection = 0;
            while (true)
            {
                var form = new CommandForm(item, state.Settings, typed);
                Settings preview = form.Preview();
                IEnumerable<string> rows = new[] { "Run" }
                    .Concat(own.Select(option => form.Row(option, preview)))
                    .Concat(groups.Select(group => $"{group.Name} settings ({group.Options.Count}) >"));
                if (ConsoleUi.Choose(preview, Title(item, state.Settings, form), rows.ToList(), selection) is not int choice)
                {
                    return;
                }
                selection = choice;
                if (choice == 0)
                {
                    Run(state, item, typed);
                }
                else if (choice <= own.Count)
                {
                    Edit(form, own[choice - 1], typed);
                }
                else
                {
                    EditGroup(state, item, typed, groups[choice - own.Count - 1]);
                }
            }
        }

        // Parses the form's command line as the command line would, then runs the command, with a heartbeat while it is quiet,
        // and waits for Enter.
        public static void Run(MenuState state, CommandItem item, IReadOnlyDictionary<Option, string> typed)
        {
            var form = new CommandForm(item, state.Settings, typed);
            Console.Clear();
            ConsoleUi.PrintHeader();
            ConsoleUi.WriteLineColoured(ConsoleColor.DarkGray, " $ " + CommandForm.Shell(form.Equivalent()));
            Console.WriteLine();
            if (CommandArgs.Parse(item.Command, form.Line(), out string error, state.Settings) is not CommandArgs args)
            {
                ConsoleUi.WriteLineColoured(ConsoleColor.Red, " " + error);
                ConsoleInput.WaitForEnter(" Press enter to return to the form.");
                return;
            }
            ExitCode exit;
            using (Heartbeat.Start())
            {
                exit = item.Run(args);
            }
            if (exit != ExitCode.Success)
            {
                Console.WriteLine();
                ConsoleUi.WriteLineColoured(ConsoleColor.Yellow, $" Ended with exit code {(int)exit} ({Describe(exit)}).");
            }
            ConsoleInput.WaitForEnter("Press enter to continue.");
            item.Then(state);
        }

        private static string Title(CommandItem item, Settings settings, CommandForm form) =>
            $"{item.Label(settings)}\n {item.Command.Summary}\n $ {CommandForm.Shell(form.Equivalent())}";

        private static void EditGroup(MenuState state, CommandItem item, Dictionary<Option, string> typed, (string Name, List<Option> Options) group)
        {
            int selection = 0;
            while (true)
            {
                var form = new CommandForm(item, state.Settings, typed);
                Settings preview = form.Preview();
                if (ConsoleUi.Choose(preview, $"{item.Label(state.Settings)} > {group.Name} settings", group.Options.Select(option => form.Row(option, preview)).ToList(), selection) is not int choice)
                {
                    return;
                }
                selection = choice;
                Edit(form, group.Options[choice], typed);
            }
        }

        // A setting is changed as the settings menu changes it, on a copy; a choice of words is offered; anything else
        // is typed, and leaving it empty takes the option off the form.
        private static void Edit(CommandForm form, Option option, Dictionary<Option, string> typed)
        {
            Settings preview = form.Preview();
            if (SettingOptions.For(option) is SettingOption setting)
            {
                Settings changed = preview.Copy();
                setting.Edit(changed);
                if (setting.Text(changed) != setting.Text(preview))
                {
                    typed[option] = setting.Text(changed);
                }
                return;
            }
            bool required = form.Command.Required.Contains(option);
            if (option.Words.Count > 0)
            {
                const string Unset = "(not given)";
                List<string> words = option.Words.Concat(required ? Array.Empty<string>() : new[] { Unset }).ToList();
                string current = form.Text(option) ?? Unset;
                if (ConsoleUi.Choose(preview, $"{option.Flag}: {option.Help}", words, Math.Max(0, words.IndexOf(current))) is int choice)
                {
                    SetOrClear(typed, option, words[choice] == Unset ? "" : words[choice]);
                }
                return;
            }
            ConsoleInput.PromptUntilAccepted($"{option.Flag} {option.Placeholder}", option.Invalid, input =>
            {
                if (input.Trim().Length == 0)
                {
                    typed.Remove(option);
                    return true;
                }
                if (!option.TryRead(input, out _, out _))
                {
                    return false;
                }
                typed[option] = input.Trim();
                return true;
            }, new[] { Capitalised(option.Help) + ".", form.Text(option) is string text ? $"Now: {text}" : "", required ? "" : "Leave it empty for the default." }
                .Where(note => note.Length > 0).ToArray());
        }

        private static void SetOrClear(Dictionary<Option, string> typed, Option option, string text)
        {
            if (text.Length == 0)
            {
                typed.Remove(option);
            }
            else
            {
                typed[option] = text;
            }
        }

        private static string Capitalised(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

        private static string Describe(ExitCode exit) => exit switch
        {
            ExitCode.Usage => "the command could not use its input",
            ExitCode.Unsolved => "nothing was solved, or a proof found a counterexample",
            ExitCode.Differs => "an outside tool's check differs from our engine",
            _ => exit.ToString(),
        };
    }
}
