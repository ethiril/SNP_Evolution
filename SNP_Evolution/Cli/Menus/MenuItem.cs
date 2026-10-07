using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // What every menu shares: the settings in use, which the settings menu and a redone run can replace, and the
    // options typed into each command's form, kept while the program runs.
    internal sealed class MenuState
    {
        public Settings Settings { get; set; } = new Settings();

        public Dictionary<CommandItem, Dictionary<Option, string>> Typed { get; } = new();
    }

    // One row of a menu; the label can show the current settings.
    internal abstract class MenuItem
    {
        protected MenuItem(Func<Settings, string> label)
        {
            Label = label;
        }

        public Func<Settings, string> Label { get; }

        public abstract void Open(MenuState state);
    }

    // A page of items that stays open until the user goes back.
    internal sealed class MenuPage : MenuItem
    {
        public MenuPage(string title, params MenuItem[] items) : base(_ => title + " >")
        {
            Title = title;
            Items = items;
        }

        public string Title { get; }

        public IReadOnlyList<MenuItem> Items { get; }

        public override void Open(MenuState state)
        {
            int selection = 0;
            while (ConsoleUi.Choose(state.Settings, Title, Items.Select(item => item.Label(state.Settings)).ToList(), selection) is int choice)
            {
                selection = choice;
                Items[choice].Open(state);
            }
        }

        // Every command item on this page and the pages under it.
        public IEnumerable<CommandItem> Commands() =>
            Items.SelectMany(item => item switch
            {
                CommandItem command => new[] { command },
                MenuPage page => page.Commands(),
                _ => Enumerable.Empty<CommandItem>(),
            });
    }

    // A screen of its own, such as the settings or the saved runs.
    internal sealed class ScreenItem : MenuItem
    {
        private readonly Action<MenuState> open;

        public ScreenItem(string label, Action<MenuState> open) : base(_ => label)
        {
            this.open = open;
        }

        public override void Open(MenuState state) => open(state);
    }

    // A command run from the menu: its options are filled in on a form, then it runs as it does from the command line,
    // starting from the menu's settings. Presets fill options from the settings each time the form opens; what the user
    // types there wins over them.
    internal class CommandItem : MenuItem
    {
        private readonly Func<Settings, IEnumerable<(Option Option, string Text)>> presets;

        public CommandItem(Func<Settings, string> label, Command command, Func<Settings, IEnumerable<(Option, string)>>? presets = null) : base(label)
        {
            Command = command;
            this.presets = presets ?? (_ => Enumerable.Empty<(Option, string)>());
        }

        public CommandItem(string label, Command command, Func<Settings, IEnumerable<(Option, string)>>? presets = null) : this(_ => label, command, presets)
        {
        }

        public Command Command { get; }

        public IEnumerable<(Option Option, string Text)> Presets(Settings settings) => presets(settings);

        public override void Open(MenuState state) => CommandScreen.Show(state, this);

        // Runs the command; the menu waits for Enter after it, then calls Then.
        public virtual ExitCode Run(CommandArgs args) => Command.Run(args);

        public virtual void Then(MenuState state)
        {
        }
    }
}
