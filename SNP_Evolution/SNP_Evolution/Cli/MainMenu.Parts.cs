using System;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Simulation;

namespace SnpEvolution.Cli
{
    internal sealed partial class MainMenu
    {
        // Runs evolve-parts on every first-part contract the library folder has no part for (see PartsSession).
        private void EvolveParts()
        {
            int seed = 1;
            bool accepted = false;
            ConsoleUi.PromptUntilAccepted("Seed", "Number was not a whole number of 1 or more.", input =>
            {
                if (input.Trim().Length == 0)
                {
                    return accepted = true;
                }
                return accepted = InputParsing.TryNonNegativeInt(input.Trim(), out seed) && seed >= 1;
            }, $"Library folder: {settings.PartLibraryFolder}", "The same seed always evolves the same parts. Leave it empty for 1.");
            if (!accepted)
            {
                return;
            }
            Console.Clear();
            ConsoleUi.PrintHeader();
            Console.WriteLine(" Evolving a part for each of the {0} first-part contracts the library has no part for, with seed {1}.", FirstParts.Contracts.Count, seed);
            Console.WriteLine(" Each is verified on the exhaustive engine, shrunk on hardware cost and saved to {0}.", settings.PartLibraryFolder);
            Console.WriteLine(" Up to {0} evaluations are spent searching for each part.", settings.PartBudget);
            Console.WriteLine();
            if (!ConsoleUi.WaitForEnterOrEscape(" Press enter to start, or ESC to go back."))
            {
                return;
            }
            var options = new PartsSession.Options(seed, settings.PartBudget, FirstParts.Contracts, settings.PartLibraryFolder, () => new ExhaustiveCpuEngine(), Redo: false,
                HardwareProfile: settings.HardwareProfile);
            PartsSession.Run(options, Console.WriteLine);
            ConsoleUi.WaitForEnter("Press enter to continue.");
        }
    }
}
