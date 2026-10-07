using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // The options a command line gave, read against the command's declarations, and the settings the command starts
    // from: the defaults on the command line, the settings in use in the menu.
    internal sealed class CommandArgs
    {
        private readonly Dictionary<Option, object> values;
        private readonly Settings starting;

        private CommandArgs(IReadOnlyList<string> line, Dictionary<Option, object> values, Settings starting)
        {
            Line = line;
            this.values = values;
            this.starting = starting;
        }

        // The whole command line, command name first, as it was given.
        public IReadOnlyList<string> Line { get; }

        public T Get<T>(Option<T> option, T fallback) where T : notnull => values.TryGetValue(option, out object? value) ? (T)value : fallback;

        public T? Find<T>(Option<T> option) where T : class => values.TryGetValue(option, out object? value) ? (T)value : null;

        // A copy of the settings the command starts from, for its options to change.
        public Settings StartingSettings() => starting.Copy();

        public bool TryGet<T>(Option<T> option, out T value) where T : notnull
        {
            bool given = values.TryGetValue(option, out object? found);
            value = given ? (T)found! : default!;
            return given;
        }

        // Every option must be one the command declares, given once, with a value it accepts; error names what is wrong.
        public static CommandArgs? Parse(Command command, IReadOnlyList<string> line, out string error, Settings? settings = null)
        {
            var values = new Dictionary<Option, object>();
            for (int index = 1; index < line.Count; index += 2)
            {
                string token = line[index];
                Option? option = token.StartsWith("--", StringComparison.Ordinal)
                    ? command.Options.FirstOrDefault(each => string.Equals(each.Flag, token, StringComparison.OrdinalIgnoreCase))
                    : null;
                if (option == null)
                {
                    error = token.StartsWith("--", StringComparison.Ordinal)
                        ? $"{command.Name} has no option {token}."
                        : $"'{token}' is not an option; options start with --.";
                    return null;
                }
                if (index + 1 >= line.Count || line[index + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    error = $"{option.Flag} needs a value: {option.Usage}.";
                    return null;
                }
                if (values.ContainsKey(option))
                {
                    error = $"{option.Flag} is given more than once.";
                    return null;
                }
                if (!option.TryRead(line[index + 1], out object? value, out string problem))
                {
                    error = problem;
                    return null;
                }
                values[option] = value!;
            }
            if (command.Required.FirstOrDefault(option => !values.ContainsKey(option)) is Option missing)
            {
                error = $"{command.Name} needs {missing.Usage}.";
                return null;
            }
            error = "";
            return new CommandArgs(line, values, command.Starting(settings ?? new Settings()));
        }
    }
}
