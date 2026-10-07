using System;
using SnpEvolution.Application;
using SnpEvolution.Evolution.Contracts;

namespace SnpEvolution.Cli
{
    // Runs evolve-parts on every first-part contract the library folder has no part for (see PartsService).
    internal static class PartsMenu
    {
        public static void EvolveParts(Settings settings)
        {
            int seed = RunSeed.Repeatable;
            bool accepted = false;
            ConsoleInput.PromptUntilAccepted("Seed", "Number was not a whole number of 1 or more.", input =>
            {
                if (input.Trim().Length == 0)
                {
                    return accepted = true;
                }
                return accepted = InputParsing.TryPositiveInt(input.Trim(), out seed);
            }, $"Library folder: {settings.PartLibraryFolder}", $"The same seed always evolves the same parts. Leave it empty for {RunSeed.Repeatable}.");
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
            if (!ConsoleInput.WaitForEnterOrEscape(" Press enter to start, or ESC to go back."))
            {
                return;
            }
            if (PartsService.Run(settings, new PartsRequest(FirstParts.Contracts, seed), Console.WriteLine).Error is string error)
            {
                Console.WriteLine(error);
            }
            ConsoleInput.WaitForEnter("Press enter to continue.");
        }
    }
}
