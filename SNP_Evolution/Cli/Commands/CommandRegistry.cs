using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SnpEvolution.Cli
{
    // Every command the command line runs, in the order usage and the README list them.
    internal static class CommandRegistry
    {
        public static readonly IReadOnlyList<Command> All = new Command[]
        {
            new EvolveCommand(),
            new AdviseCommand(),
            new CompileCommand(),
            new ReachCommand(),
            new EvolvePartsCommand(),
            new ComposeCommand(),
            new VerifyCommand(),
            new PartsCommand(),
            new ExportVerilogCommand(),
            new ExportNirCommand(),
            new ExportUppaalCommand(),
            new BenchmarkCommand(),
            new SelectCommand(),
            new RunCommand(),
            new TasksCommand(),
            new AlgorithmsCommand(),
        };

        public static T Get<T>() where T : Command => All.OfType<T>().Single();

        public static Command? Find(string name) => All.FirstOrDefault(command => string.Equals(command.Name, name, StringComparison.OrdinalIgnoreCase));

        public static string Usage => "Usage:\n" + string.Join("\n", All.Select(command => "  " + command.Usage));

        // The README's command table: each command, what it does and its options, with the required ones first.
        public static string MarkdownTable()
        {
            var table = new StringBuilder();
            table.AppendLine("| Command | What it does | Options |");
            table.AppendLine("|---|---|---|");
            foreach (Command command in All)
            {
                IEnumerable<string> options = command.Required.Select(option => $"`{option.Usage}`")
                    .Concat(command.Options.Except(command.Required).Select(option => $"`[{option.Usage}]`"));
                table.AppendLine($"| `{command.Name}` | {command.Summary} | {string.Join(" ", options).Replace("|", "\\|")} |");
            }
            return table.ToString();
        }
    }
}
