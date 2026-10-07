using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Networks;

namespace SnpEvolution.Storage
{
    // A part library folder holds one JSON file per part, named after its contract (delay-1.json, zero-test.json), so a
    // save rewrites the same files and the folder reads well as a diff. Each file has the network in the format
    // NetworkFiles writes, the contract, the port binding, the hardware cost, where the part came from and the bound it is
    // proven to; a promoted part's file holds its recipe instead.
    public static class PartLibraryFiles
    {
        public const string Extension = ".json";

        public static string FileName(Contract contract) => Stem(contract.Name) + Extension;

        // "delay 2" becomes delay-2, so files written for a part are named alike.
        public static string Stem(string name) => string.Concat(name.ToLowerInvariant().Select(letter => char.IsLetterOrDigit(letter) ? letter : '-'));

        public static string ToJson(LibraryPart part) => Json.Write(PartFile.Of(part)) + "\n";

        // Writes every contract part in the library, and returns the paths written.
        public static IReadOnlyList<string> Save(ModuleLibrary library, string folder)
        {
            Directory.CreateDirectory(folder);
            var written = new List<string>();
            foreach (LibraryPart part in library.Parts.Select(module => module.Part!))
            {
                string path = Path.Combine(folder, FileName(part.Contract));
                File.WriteAllText(path, ToJson(part));
                written.Add(path);
            }
            return written;
        }

        // Every part in the folder, re-verified on the exhaustive engine, in file name order, with promoted parts after the
        // parts they are built from; an empty library when the folder does not exist. Throws InvalidDataException naming
        // every file that cannot be read, whose contract differs from the known contract of the same name, whose part
        // fails its contract or has a counterexample recorded, or whose recipe names a part the folder does not have.
        public static ModuleLibrary Load(string folder, Action<string>? log = null)
        {
            var library = new ModuleLibrary(log: log);
            if (!Directory.Exists(folder))
            {
                return library;
            }
            var problems = new List<string>();
            var waiting = new List<(string Name, PartFile File)>();
            foreach (string path in Directory.GetFiles(folder, "*" + Extension).OrderBy(path => path, StringComparer.Ordinal))
            {
                string name = Path.GetFileName(path);
                try
                {
                    waiting.Add((name, Parse(File.ReadAllText(path), name)));
                }
                catch (InvalidDataException exception)
                {
                    problems.Add(exception.Message);
                }
            }
            AddInDependencyOrder(waiting, library, problems);
            if (problems.Count > 0)
            {
                throw new InvalidDataException($"The part library in '{folder}' has parts that cannot be used: " + string.Join(" ", problems));
            }
            return library;
        }

        // A pass adds every file whose children are in; one that adds nothing leaves only files that cannot be built.
        private static void AddInDependencyOrder(List<(string Name, PartFile File)> waiting, ModuleLibrary library, List<string> problems)
        {
            while (waiting.Count > 0)
            {
                HashSet<string> kept = library.Parts.Select(module => module.Part!.Contract.Name).ToHashSet();
                List<(string Name, PartFile File)> ready = waiting.Where(each => each.File.Recipe?.Children.All(kept.Contains) ?? true).ToList();
                if (ready.Count == 0)
                {
                    problems.AddRange(waiting.Select(each => $"Part file '{each.Name}' is built from parts the folder does not have: " +
                        string.Join(", ", each.File.Recipe!.Children.Where(child => !kept.Contains(child))) + "."));
                    return;
                }
                foreach ((string name, PartFile file) in ready)
                {
                    waiting.Remove((name, file));
                    try
                    {
                        LibraryPart part = Build(file, name, library);
                        library.AddPart(part, $"{name} ({part.Origin.Run})");
                    }
                    catch (InvalidDataException exception)
                    {
                        problems.Add(exception.Message);
                    }
                }
            }
        }

        // The part a file holds, with its cost and behaviour measured again rather than taken from the file.
        public static LibraryPart Read(string json, string name, ModuleLibrary? library = null) => Build(Parse(json, name), name, library ?? new ModuleLibrary());

        private static PartFile Parse(string json, string name)
        {
            PartFile? file;
            try
            {
                file = Json.Read<PartFile>(json);
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException)
            {
                throw new InvalidDataException($"Part file '{name}' is not valid: {exception.Message}");
            }
            if (file?.Contract == null || (file.Network == null && file.Recipe == null) || (file.Binding == null && file.Recipe == null) || file.Origin == null)
            {
                throw new InvalidDataException($"Part file '{name}' is missing its contract, network or recipe, binding or origin.");
            }
            if (file.Contract.Problems() is { Count: > 0 } problems)
            {
                throw new InvalidDataException($"Part file '{name}' has a malformed contract: {string.Join(" ", problems)}");
            }
            Contract? given = ArithmeticParts.Known.FirstOrDefault(contract => contract.Name == file.Contract.Name);
            if (given != null && !given.SameAs(file.Contract))
            {
                throw new InvalidDataException($"Part file '{name}' changes the contract '{given.Name}'.");
            }
            return file;
        }

        private static LibraryPart Build(PartFile file, string name, ModuleLibrary library)
        {
            Part part;
            try
            {
                if (file.Recipe is PartRecipe recipe)
                {
                    (Composition composition, PortBinding binding) = recipe.Build(library);
                    part = new Part(file.Contract, composition.Flatten(library), binding);
                }
                else
                {
                    part = new Part(file.Contract, file.Network!, file.Binding!);
                }
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException($"Part file '{name}' cannot be built: {exception.Message}");
            }
            PartMeasurement measurement;
            try
            {
                measurement = Verifier.Measure(part, new EvaluationBudget());
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException($"Part file '{name}' does not fit its contract: {exception.Message}");
            }
            if (measurement.Verdict is not Verdict.Passed)
            {
                throw new InvalidDataException($"Part file '{name}' fails its contract '{file.Contract.Name}': {measurement.Description.Replace(Environment.NewLine, "; ")}.");
            }
            if (file.Proven?.FailsAt is string input)
            {
                throw new InvalidDataException($"Part file '{name}' fails its contract '{file.Contract.Name}' at {input}, as its bounded check found.");
            }
            return measurement.ToLibraryPart(part, file.Origin) with { Recipe = file.Recipe, Proven = file.Proven };
        }

        // Cost and Latency are written for readers of the folder; loading measures them again. Proven is kept as written,
        // since checking a bound again takes as long as the verify command spent on it.
        private sealed record PartFile(
            Contract Contract,
            [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] PortBinding? Binding,
            [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] Network? Network,
            [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] PartRecipe? Recipe,
            HardwareCost? Cost,
            int? Latency,
            PartOrigin Origin,
            [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] ProvenBound? Proven = null)
        {
            public static PartFile Of(LibraryPart part) => part.Recipe != null
                ? new PartFile(part.Contract, null, null, part.Recipe, part.Cost, part.Latency, part.Origin, part.Proven)
                : new PartFile(part.Contract, part.Part.Binding, part.Part.Network, null, part.Cost, part.Latency, part.Origin, part.Proven);
        }
    }
}
