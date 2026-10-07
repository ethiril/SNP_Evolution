using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // The parts in the settings' library folder; choosing one shows it in full with the parts command.
    internal static class PartsBrowser
    {
        public static void Show(MenuState state)
        {
            int selection = 0;
            while (true)
            {
                Loaded<IReadOnlyList<PartFile>> loaded = PartLibraries.PartsIn(state.Settings.PartLibraryFolder);
                if (loaded.Value is not { Count: > 0 } parts)
                {
                    Console.Clear();
                    ConsoleUi.PrintHeader();
                    Console.WriteLine(" " + (loaded.Error ?? $"No parts are kept in {state.Settings.PartLibraryFolder} yet. Evolve the first library parts to add some."));
                    ConsoleInput.WaitForEnter(" Press enter to return to the menu.");
                    return;
                }
                List<PartFile> sorted = parts.OrderBy(file => file.Part.Contract.Name, StringComparer.Ordinal).ToList();
                List<string> rows = sorted.Select(file => ConsoleUi.Row(file.Part.Contract.Name,
                    $"{file.Part.Cost.Neurons} neurons, {file.Part.Cost.Synapses} synapses, latency {file.Part.Latency}")).ToList();
                if (ConsoleUi.Choose(state.Settings, $"Kept parts in {state.Settings.PartLibraryFolder}", rows, selection) is not int choice)
                {
                    return;
                }
                selection = choice;
                var item = new CommandItem(sorted[choice].Part.Contract.Name, CommandRegistry.Get<PartsCommand>(), _ => new[] { ((Option)CommonOptions.Part, sorted[choice].Path) });
                CommandScreen.Run(state, item, new Dictionary<Option, string>());
            }
        }
    }
}
