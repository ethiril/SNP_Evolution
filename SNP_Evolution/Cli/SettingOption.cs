using System;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // A setting that is both a command-line option and a row of the settings menu, declared once for both.
    internal abstract class SettingOption
    {
        protected SettingOption(string label)
        {
            Label = label;
        }

        // The menu row's label.
        public string Label { get; }

        public abstract Option Option { get; }

        public string Row(Settings settings) => ConsoleUi.Row(Label, Shown(settings));

        // The setting's value as the menu shows it.
        public abstract string Shown(Settings settings);

        // The setting as the option's value, which the option reads back.
        public abstract string Text(Settings settings);

        // Sets the setting from the command line, when the option was given.
        public abstract void ApplyFrom(CommandArgs args, Settings settings);

        // Sets the setting from the option's value; false when the option cannot take the text.
        public abstract bool TryApply(Settings settings, string text);

        // Changes the setting from the menu: a switch flips, a choice of words is offered, anything else is asked for.
        public abstract void Edit(Settings settings);
    }

    internal sealed class SettingOption<T> : SettingOption where T : notnull
    {
        private readonly Func<Settings, T> get;
        private readonly Action<Settings, T> set;
        private readonly Func<Settings, object>? show;
        private readonly string? prompt;
        private readonly Action<Settings>? edit;

        // prompt is what the menu asks; show is the row's value when it is not simply the setting; edit replaces the
        // menu's flip, choice or prompt.
        public SettingOption(Option<T> option, string label, Func<Settings, T> get, Action<Settings, T> set,
            string? prompt = null, Func<Settings, object>? show = null, Action<Settings>? edit = null) : base(label)
        {
            Typed = option;
            this.get = get;
            this.set = set;
            this.prompt = prompt;
            this.show = show;
            this.edit = edit;
        }

        public Option<T> Typed { get; }

        public override Option Option => Typed;

        public override string Shown(Settings settings) =>
            show?.Invoke(settings).ToString() ?? (get(settings) is bool on ? (on ? "on" : "off") : Text(settings));

        public override string Text(Settings settings) => Typed.Kind.Format(get(settings));

        public override void ApplyFrom(CommandArgs args, Settings settings)
        {
            if (args.TryGet(Typed, out T value))
            {
                set(settings, value);
            }
        }

        public override bool TryApply(Settings settings, string text)
        {
            if (!Typed.Kind.Parse(text, out T value))
            {
                return false;
            }
            set(settings, value);
            return true;
        }

        public override void Edit(Settings settings)
        {
            if (edit != null)
            {
                edit(settings);
            }
            else if (get(settings) is bool on)
            {
                set(settings, (T)(object)!on);
            }
            else if (Typed.Kind.Words.Count > 0)
            {
                string word = MenuPrompts.Choose(settings, prompt ?? Label, Typed.Kind.Words, Text(settings), each => each);
                TryApply(settings, word);
            }
            else
            {
                MenuPrompts.PromptFor(prompt ?? Label, Typed.Kind.Invalid, Typed.Kind.Parse, value => set(settings, value));
            }
        }
    }
}
