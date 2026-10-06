using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Proposals;
using SnpEvolution.Networks;
using SnpEvolution.Storage;

namespace SnpEvolution.Cli
{
    // The part library a composition run builds from: loading it, saving what the run added, and reporting what was reused.
    internal static class CompositionParts
    {
        public static ModuleLibrary Load(Settings settings, Action<string> log)
        {
            ModuleLibrary parts = PartLibraryFiles.Load(settings.PartLibraryFolder, log);
            if (settings.HandBuiltParts)
            {
                HandBuiltMachines.AddParts(parts, log, settings.HandBuiltAddLoop);
            }
            return parts;
        }

        // The default folder holds only parts runs found, so a library with hand-built parts never goes there.
        public static void SaveIfGrown(Settings settings, ModuleLibrary? parts, int partsAtStart, Action<string> log)
        {
            if (parts == null || parts.Parts.Count == partsAtStart)
            {
                return;
            }
            if (settings.HandBuiltParts && Path.GetFullPath(settings.PartLibraryFolder) == Path.GetFullPath(Settings.DefaultPartLibraryFolder()))
            {
                log("The library holds hand-built parts, so it is not saved to the default part library folder; give another folder to keep it.");
                return;
            }
            IReadOnlyList<string> written = PartLibraryFiles.Save(parts, settings.PartLibraryFolder);
            log($"Saved {written.Count} part(s) to {settings.PartLibraryFolder}.");
        }

        public static string Report(IGeneticAlgorithm geneticAlgorithm, PartProposals proposals) =>
            $"{proposals.Describe()}{Environment.NewLine}{Environment.NewLine}{PartReuse.Describe(geneticAlgorithm.Best?.Genes, geneticAlgorithm.Population.Select(individual => individual.Genes), proposals.Library)}"
            + (geneticAlgorithm.Best is Individual composed ? Environment.NewLine + UsesPromoted(composed.Genes, proposals.Library) : "");

        // A best network built on a promoted part is the sign that the library compounds.
        private static string UsesPromoted(Network network, ModuleLibrary library)
        {
            List<string> promoted = PartReuse.Count(network, library)
                .Where(count => count.Direct > 0 && library.PartFor(count.Contract)?.Part!.IsComposite == true)
                .Select(count => $"{count.Contract} x{count.Direct}")
                .ToList();
            return promoted.Count > 0 ? $"The best network reuses promoted part(s): {string.Join(", ", promoted)}." : "The best network uses no promoted part.";
        }
    }
}
