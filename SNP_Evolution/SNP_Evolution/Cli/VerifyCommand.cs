using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Storage;
using static SnpEvolution.Cli.CommandOptions;

namespace SnpEvolution.Cli
{
    // verify: exits with 1 for a bad command or library and 2 when a part has a counterexample; --only matches any contract name containing it, ignoring case.
    internal static class VerifyCommand
    {
        internal const int Refuted = 2;

        internal static int Run(IReadOnlyDictionary<string, string> options, Settings settings)
        {
            var limits = new ProofLimits(TimeSpan.FromSeconds(Number(options, "seconds", 60)), (int)Math.Min(int.MaxValue, Number(options, "bound", int.MaxValue)));
            List<(LibraryPart Part, string Path)> parts;
            try
            {
                parts = options.GetValueOrDefault("part") is string file
                    ? new List<(LibraryPart, string)> { (PartLibraryFiles.Read(File.ReadAllText(file), Path.GetFileName(file)), file) }
                    : Library(options.GetValueOrDefault("library", settings.PartLibraryFolder));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }
            if (options.GetValueOrDefault("only") is string only)
            {
                string[] names = only.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                parts = parts.Where(each => names.Any(name => each.Part.Contract.Name.Contains(name, StringComparison.OrdinalIgnoreCase))).ToList();
            }
            if (parts.Count == 0)
            {
                Console.Error.WriteLine("There are no parts to verify.");
                return 1;
            }
            bool refuted = false;
            foreach ((LibraryPart part, string path) in parts)
            {
                var clock = Stopwatch.StartNew();
                BoundedResult result = BoundedCheck.Prove(part.Part, limits);
                Console.WriteLine($"{part.Contract.Name}: {result.Proven} in {clock.Elapsed.TotalSeconds:0.0} s.");
                if (result.Verdict is Verdict.Failed failed)
                {
                    Console.WriteLine(CounterexampleText.Of(part.Part, failed.Counterexample));
                    refuted = true;
                }
                File.WriteAllText(path, PartLibraryFiles.ToJson(part with { Proven = result.Proven }));
            }
            return refuted ? Refuted : 0;
        }

        // Each part with the file it is saved to, which is the file it was loaded from.
        private static List<(LibraryPart Part, string Path)> Library(string folder)
        {
            if (!Directory.Exists(folder))
            {
                throw new DirectoryNotFoundException($"There is no part library folder '{folder}'.");
            }
            ModuleLibrary library = PartLibraryFiles.Load(folder);
            return library.Parts.Select(module => module.Part).OfType<LibraryPart>()
                .Select(part => (part, Path.Combine(folder, PartLibraryFiles.FileName(part.Contract)))).ToList();
        }
    }
}
