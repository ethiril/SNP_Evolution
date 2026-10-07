using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // The menu runs the same commands as the command line, each from a form of its options; a test checks every
    // command is on it. Pages stay open until the user goes back, so several things can be done in a row.
    internal static class MainMenu
    {
        public static readonly MenuPage Root = new MenuPage("",
            new MenuPage("Evolve a system",
                new EvolveItem(settings => settings.Task == Catalog.TargetTask ? $"Match the target: {settings.Target.Kind} {settings.Target}" : $"For the task: {settings.Task.Name}", Selected),
                new EvolveItem("Starting from the natural numbers network", settings => TargetOf(settings).Append((EvolveCommand.Start, "natural"))),
                new EvolveItem("Starting from the even numbers network", settings => TargetOf(settings).Append((EvolveCommand.Start, "even"))),
                new AdviseItem(settings => $"Suggest settings for: {Describe(settings)}", Selected),
                new CommandItem("Compile a target, then shrink it", CommandRegistry.Get<CompileCommand>(), TargetOf),
                new CommandItem("Compare setups on a target, with one budget", CommandRegistry.Get<ReachCommand>(), TargetOf),
                new ScreenItem("Redo a saved run >", RedoMenu.Show)),
            new MenuPage("Library parts",
                new CommandItem("Evolve the first library parts", CommandRegistry.Get<EvolvePartsCommand>()),
                new CommandItem("Compose a suite task from parts", CommandRegistry.Get<ComposeCommand>(), SuiteTask),
                new CommandItem("Prove the parts' contracts up to a bound", CommandRegistry.Get<VerifyCommand>())),
            new MenuPage("Run a network",
                new CommandItem("Natural numbers network", CommandRegistry.Get<RunCommand>(), _ => new[] { ((Option)RunCommand.Network, "natural") }),
                new CommandItem("Even numbers network", CommandRegistry.Get<RunCommand>(), _ => new[] { ((Option)RunCommand.Network, "even") }),
                new CommandItem("A network from a file", CommandRegistry.Get<RunCommand>())),
            new MenuPage("Export",
                new CommandItem("Verilog, co-simulated under iverilog", CommandRegistry.Get<ExportVerilogCommand>()),
                new CommandItem("NIR, co-simulated in norse", CommandRegistry.Get<ExportNirCommand>()),
                new CommandItem("Uppaal timed automata, model-checked", CommandRegistry.Get<ExportUppaalCommand>())),
            new MenuPage("Benchmark",
                new BenchmarkItem("Every algorithm on the task suite"),
                new SelectItem(_ => "Find the best algorithm for a task", SuiteTask),
                new CommandItem("List the suite tasks", CommandRegistry.Get<TasksCommand>()),
                new CommandItem("List the searches", CommandRegistry.Get<AlgorithmsCommand>())),
            new ScreenItem("Settings >", state => state.Settings = SettingsMenu.Edit(state.Settings)));

        public static void Run()
        {
            var state = new MenuState();
            List<string> labels = Root.Items.Select(item => item.Label(state.Settings)).Append("Quit").ToList();
            int selection = 0;
            while (true)
            {
                int? choice = ConsoleUi.Choose(state.Settings, "", labels, selection, splash: true);
                selection = choice ?? selection;
                if (choice is int open && open < Root.Items.Count)
                {
                    Root.Items[open].Open(state);
                }
                else if (ConsoleUi.Confirm(state.Settings, "Are you sure you wish to quit?"))
                {
                    return;
                }
            }
        }

        // The selected task: the target as --target and --kind, or a suite task as --task.
        internal static IEnumerable<(Option, string)> Selected(Settings settings) =>
            settings.Task == Catalog.TargetTask ? TargetOf(settings) : SuiteTask(settings);

        internal static IEnumerable<(Option, string)> TargetOf(Settings settings) => new (Option, string)[]
        {
            (CommonOptions.Target, TargetArgs.Text(settings.Target)),
            (CommonOptions.Kind, CommonOptions.Kind.Kind.Format(settings.Target.Kind)),
        };

        private static IEnumerable<(Option, string)> SuiteTask(Settings settings) =>
            settings.Task == Catalog.TargetTask ? Enumerable.Empty<(Option, string)>() : new (Option, string)[] { (CommonOptions.Task, settings.Task.Name) };

        private static string Describe(Settings settings) => settings.Task == Catalog.TargetTask ? $"{settings.Target.Kind} {settings.Target}" : settings.Task.Name;
    }
}
