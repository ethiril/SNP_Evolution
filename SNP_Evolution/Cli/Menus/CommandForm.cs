using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // A command's options as its menu form holds them: what the user typed, else the item's presets, else nothing; and
    // the command line they make, which the menu parses and runs as the command line does.
    internal sealed class CommandForm
    {
        private readonly Settings settings;
        private readonly IReadOnlyDictionary<Option, string> typed;
        private readonly Dictionary<Option, string> presets;

        public CommandForm(CommandItem item, Settings settings, IReadOnlyDictionary<Option, string> typed)
        {
            Command = item.Command;
            this.settings = settings;
            this.typed = typed;
            presets = item.Presets(settings).ToDictionary(preset => preset.Option, preset => preset.Text);
        }

        public Command Command { get; }

        // The option's value on the form, or null when it is left to the command. A required setting the form has no
        // value for takes the setting's.
        public string? Text(Option option) =>
            typed.TryGetValue(option, out string? text) ? text
            : presets.TryGetValue(option, out string? preset) ? preset
            : Command.Required.Contains(option) && SettingOptions.For(option) is SettingOption setting ? setting.Text(Preview())
            : null;

        // The command line, command name first, with the options in the order the command declares them.
        public IReadOnlyList<string> Line() => Lines(Command.Options.Where(option => Text(option) != null));

        // The settings the command would start from, with the settings options on the form applied.
        public Settings Preview()
        {
            Settings preview = Command.Starting(settings);
            foreach (Option option in Command.Options.Where(option => typed.ContainsKey(option) || presets.ContainsKey(option)))
            {
                SettingOptions.For(option)?.TryApply(preview, typed.TryGetValue(option, out string? text) ? text : presets[option]);
            }
            return preview;
        }

        // The command line for the same run outside the menu: the form's options, and each setting the command takes
        // whose value in the menu is not its default.
        public IReadOnlyList<string> Equivalent()
        {
            Settings preview = Preview();
            Settings defaults = Command.Starting(new Settings());
            return Lines(Command.Options.Where(option => Text(option) != null ||
                SettingOptions.For(option) is SettingOption setting && setting.Text(preview) != setting.Text(defaults) && setting.Text(preview).Length > 0),
                option => Text(option) ?? SettingOptions.For(option)!.Text(preview));
        }

        // The row for the option: its flag, and its value, the setting's, or what the command does without it.
        public string Row(Option option, Settings preview) =>
            ConsoleUi.Row(option.Flag, SettingOptions.For(option) is SettingOption setting ? setting.Shown(preview)
                : Text(option) is string text ? text
                : Command.Required.Contains(option) ? "(required)" : "(not given)");

        // The line as a shell would take it, with words that hold spaces, commas or bars quoted.
        public static string Shell(IEnumerable<string> line) =>
            "snp-evolution " + string.Join(" ", line.Select(word => word.Length == 0 || word.IndexOfAny(new[] { ' ', ',', '|', '"' }) >= 0 ? $"\"{word}\"" : word));

        private IReadOnlyList<string> Lines(IEnumerable<Option> options, System.Func<Option, string>? value = null) =>
            new[] { Command.Name }.Concat(options.SelectMany(option => new[] { option.Flag, (value ?? (each => Text(each)!))(option) })).ToList();
    }
}
