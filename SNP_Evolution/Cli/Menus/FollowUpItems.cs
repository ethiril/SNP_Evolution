using System;
using System.Collections.Generic;
using SnpEvolution.Application;
using SnpEvolution.Search;
using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Search.Fitness;

namespace SnpEvolution.Cli
{
    // Evolve, then offer to save the run under a name, so it can be redone or its settings reused.
    internal sealed class EvolveItem : CommandItem
    {
        private EvolveRun? finished;

        public EvolveItem(Func<Settings, string> label, Func<Settings, IEnumerable<(Option, string)>>? presets = null)
            : base(label, CommandRegistry.Get<EvolveCommand>(), presets)
        {
        }

        public EvolveItem(string label, Func<Settings, IEnumerable<(Option, string)>>? presets = null) : this(_ => label, presets)
        {
        }

        // The name to save under, when the run is a saved run redone.
        public string? SavedName { get; init; }

        public override ExitCode Run(CommandArgs args) => CommandRegistry.Get<EvolveCommand>().Run(args, run => finished = run);

        public override void Then(MenuState state)
        {
            if (finished is not EvolveRun run)
            {
                return;
            }
            finished = null;
            if (!ConsoleUi.Confirm(state.Settings, "Save this run, to redo it or reuse its settings later?"))
            {
                return;
            }
            string defaultName = SavedName ?? $"{EvolveService.TaskFor(run.Used, run.Start).Name} ({DateTime.Now:yyyy-MM-dd HH:mm})";
            string? name = null;
            ConsoleInput.PromptUntilAccepted("Name for this run", "", input => (name = input.Trim().Length > 0 ? input.Trim() : defaultName) != null,
                $"Leave it empty for: {defaultName}", "A saved run with the same name is replaced.", $"Saved runs are kept in {SavedRuns.Default.Path}");
            if (name != null)
            {
                SavedRuns.Default.Add(new SavedRun(name, DateTime.Now, run.Start, run.FileStem, run.Result.Outcome, run.Used));
            }
        }
    }

    // Advise, then offer to apply the suggestions to the settings in use.
    internal sealed class AdviseItem : CommandItem
    {
        private Advice? advice;

        public AdviseItem(Func<Settings, string> label, Func<Settings, IEnumerable<(Option, string)>> presets) : base(label, CommandRegistry.Get<AdviseCommand>(), presets)
        {
        }

        public override ExitCode Run(CommandArgs args) => CommandRegistry.Get<AdviseCommand>().Run(args, given => advice = given);

        public override void Then(MenuState state)
        {
            if (advice is { Suggestions.Count: > 0 } suggested && ConsoleUi.Confirm(state.Settings, "Apply these suggestions to the settings?"))
            {
                RunAdvisor.ApplyAll(state.Settings, suggested);
            }
            advice = null;
        }
    }

    // Select, then offer to evolve with the winner from now on.
    internal sealed class SelectItem : CommandItem
    {
        private ISearch<Individual>? winner;

        public SelectItem(Func<Settings, string> label, Func<Settings, IEnumerable<(Option, string)>> presets) : base(label, CommandRegistry.Get<SelectCommand>(), presets)
        {
        }

        public override ExitCode Run(CommandArgs args) => CommandRegistry.Get<SelectCommand>().Run(args, picked => winner = picked);

        public override void Then(MenuState state)
        {
            if (winner is EvolutionSearch search && ConsoleUi.Confirm(state.Settings, $"Use {search.Name} from now on?"))
            {
                state.Settings.Algorithm = search;
            }
            winner = null;
        }
    }

    // Benchmark, then save the table to a run folder.
    internal sealed class BenchmarkItem : CommandItem
    {
        public BenchmarkItem(string label) : base(label, CommandRegistry.Get<BenchmarkCommand>())
        {
        }

        public override ExitCode Run(CommandArgs args) =>
            CommandRegistry.Get<BenchmarkCommand>().Run(args, rows => Console.WriteLine("The table is saved in {0}", BenchmarkService.Save(rows)));
    }
}
