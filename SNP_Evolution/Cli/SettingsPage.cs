using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // A settings row: the label and value shown, and how choosing it changes the setting.
    internal sealed record SettingRow(Func<Settings, string> Row, Action<Settings> Edit)
    {
        public static implicit operator SettingRow(SettingOption option) => new SettingRow(option.Row, option.Edit);

        public static SettingRow Toggle(string label, Func<Settings, bool> get, Action<Settings, bool> set) =>
            new SettingRow(settings => ConsoleUi.Row(label, get(settings) ? "on" : "off"), settings => set(settings, !get(settings)));
    }

    // A page of settings that lists each with its current value and stays open until the user goes back, so several
    // can be changed in one visit.
    internal static class SettingsPage
    {
        public static void Edit(Settings settings, string title, IReadOnlyList<SettingRow> rows)
        {
            int selection = 0;
            while (ConsoleUi.Choose(settings, title, rows.Select(row => row.Row(settings)).ToList(), selection) is int choice)
            {
                selection = choice;
                rows[choice].Edit(settings);
            }
        }
    }
}
