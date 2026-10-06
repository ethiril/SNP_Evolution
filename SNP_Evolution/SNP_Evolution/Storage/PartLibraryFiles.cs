using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Networks;

namespace SnpEvolution.Storage
{
    // A part library folder holds one JSON file per part, named after its contract (delay-1.json, zero-test.json), so a
    // save rewrites the same files and the folder reads well as a diff. Each file has the network in the format
    // NetworkFiles writes, the contract, the port binding, the hardware cost and where the part came from.
    public static class PartLibraryFiles
    {
        public const string Extension = ".json";

        public static string FileName(Contract contract) =>
            string.Concat(contract.Name.ToLowerInvariant().Select(letter => char.IsLetterOrDigit(letter) ? letter : '-')) + Extension;

        public static string ToJson(LibraryPart part) => JsonConvert.SerializeObject(PartFile.Of(part), Formatting.Indented) + "\n";

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

        // Every part in the folder, re-verified on the exhaustive engine, in file name order; an empty library when the
        // folder does not exist. Throws InvalidDataException naming every file that cannot be read, whose contract
        // differs from the first-parts contract of the same name, or whose part fails its contract.
        public static ModuleLibrary Load(string folder, Action<string>? log = null)
        {
            var library = new ModuleLibrary(log: log);
            if (!Directory.Exists(folder))
            {
                return library;
            }
            var problems = new List<string>();
            foreach (string path in Directory.GetFiles(folder, "*" + Extension).OrderBy(path => path, StringComparer.Ordinal))
            {
                string name = Path.GetFileName(path);
                try
                {
                    LibraryPart part = Read(File.ReadAllText(path), name);
                    library.AddPart(part, $"{name} ({part.Origin.Run})");
                }
                catch (InvalidDataException exception)
                {
                    problems.Add(exception.Message);
                }
            }
            if (problems.Count > 0)
            {
                throw new InvalidDataException($"The part library in '{folder}' has parts that cannot be used: " + string.Join(" ", problems));
            }
            return library;
        }

        // The part a file holds, with its cost and behaviour measured again rather than taken from the file.
        public static LibraryPart Read(string json, string name)
        {
            PartFile? file;
            try
            {
                file = JsonConvert.DeserializeObject<PartFile>(json);
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException)
            {
                throw new InvalidDataException($"Part file '{name}' is not valid: {exception.Message}");
            }
            if (file?.Contract == null || file.Network == null || file.Binding == null || file.Origin == null)
            {
                throw new InvalidDataException($"Part file '{name}' is missing its contract, network, binding or origin.");
            }
            if (file.Contract.Problems() is { Count: > 0 } problems)
            {
                throw new InvalidDataException($"Part file '{name}' has a malformed contract: {string.Join(" ", problems)}");
            }
            Contract? given = FirstParts.Contracts.FirstOrDefault(contract => contract.Name == file.Contract.Name);
            if (given != null && given.ToJson() != file.Contract.ToJson())
            {
                throw new InvalidDataException($"Part file '{name}' changes the first-part contract '{given.Name}'.");
            }
            var part = new Part(file.Contract, file.Network, file.Binding);
            PartMeasurement measurement;
            try
            {
                measurement = PartEvolution.Measure(part.Network, part.Task());
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException($"Part file '{name}' does not fit its contract: {exception.Message}");
            }
            if (!measurement.MeetsContract)
            {
                throw new InvalidDataException($"Part file '{name}' fails its contract '{file.Contract.Name}': {measurement.Description.Replace(Environment.NewLine, "; ")}.");
            }
            return LibraryPart.Of(part, measurement, file.Origin);
        }

        // Cost and Latency are written for readers of the folder; loading measures them again.
        private sealed record PartFile(Contract Contract, PortBinding Binding, Network Network, HardwareCost? Cost, int? Latency, PartOrigin Origin)
        {
            public static PartFile Of(LibraryPart part) =>
                new PartFile(part.Contract, part.Part.Binding, part.Part.Network, part.Cost, part.Latency, part.Origin);
        }
    }
}
