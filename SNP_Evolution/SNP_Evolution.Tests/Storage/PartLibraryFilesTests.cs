using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Networks;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Storage
{
    public sealed class PartLibraryFilesTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "part-library-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private static LibraryPart Measured(Part part, int seed = 7) =>
            Verifier.Measure(part, new EvaluationBudget()).ToLibraryPart(part, new PartOrigin(seed, "evolve-parts --seed 1", 1234));

        private static Part FirstPartRegister()
        {
            Part register = ReferenceParts.Register();
            return register with { Contract = FirstParts.Named("register") };
        }

        private static ModuleLibrary Library(params Part[] parts)
        {
            var library = new ModuleLibrary();
            foreach (Part part in parts)
            {
                library.AddPart(Measured(part), "a test");
            }
            return library;
        }

        private static string Describe(LibraryPart part) =>
            $"{Json.Write(part.Contract)}\n{NetworkNotation.Format(part.Part.Network)}\n{string.Join(",", part.Part.Binding.Positions)}\n{part.Cost}\n{part.Latency}\n{part.Behaviour}\n{part.Origin}";

        [Fact]
        public void FilesAreNamedByContract()
        {
            Assert.Equal("delay-3.json", PartLibraryFiles.FileName(FirstParts.Named("delay 3")));
            Assert.Equal("count-to-interval.json", PartLibraryFiles.FileName(FirstParts.Named("count to interval")));
            Assert.Equal("delay-2", PartLibraryFiles.Stem("Delay 2"));
        }

        [Fact]
        public void SaveThenLoadGivesTheSameLibraryPartByPart()
        {
            ModuleLibrary original = Library(ReferenceParts.Delay(1), ReferenceParts.Delay(3), FirstPartRegister());

            IReadOnlyList<string> written = PartLibraryFiles.Save(original, folder);
            ModuleLibrary loaded = PartLibraryFiles.Load(folder);

            Assert.Equal(new[] { "delay-1.json", "delay-3.json", "register.json" }, written.Select(Path.GetFileName).OrderBy(name => name));
            Assert.Equal(
                original.Parts.Select(module => Describe(module.Part!)).OrderBy(text => text),
                loaded.Parts.Select(module => Describe(module.Part!)).OrderBy(text => text));
        }

        [Fact]
        public void SavingTwiceWritesTheSameBytes()
        {
            ModuleLibrary library = Library(ReferenceParts.Delay(2), FirstPartRegister());
            PartLibraryFiles.Save(library, folder);
            string[] first = Directory.GetFiles(folder).Order().Select(File.ReadAllText).ToArray();

            PartLibraryFiles.Save(PartLibraryFiles.Load(folder), folder);

            Assert.Equal(first, Directory.GetFiles(folder).Order().Select(File.ReadAllText).ToArray());
        }

        [Fact]
        public void TheNetworkIsWrittenInTheNetworkFileFormat()
        {
            Part delay = ReferenceParts.Delay(2);
            string json = PartLibraryFiles.ToJson(Measured(delay));

            string network = Newtonsoft.Json.Linq.JObject.Parse(json)["Network"]!.ToString();

            Assert.Equal(NetworkNotation.Format(delay.Network), NetworkNotation.Format(NetworkFiles.FromJson(network)!));
        }

        [Fact]
        public void APartEditedToBreakItsContractIsRefusedByName()
        {
            PartLibraryFiles.Save(Library(ReferenceParts.Delay(2), ReferenceParts.Delay(3)), folder);
            string path = Path.Combine(folder, "delay-3.json");
            // Done now waits one step less, so it fires too early.
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"Delay\": 2", "\"Delay\": 1"));

            var refused = Assert.Throws<InvalidDataException>(() => PartLibraryFiles.Load(folder));

            Assert.Contains("delay-3.json", refused.Message);
            Assert.DoesNotContain("delay-2.json", refused.Message);
        }

        [Fact]
        public void APartFileThatChangesAFirstPartContractIsRefused()
        {
            PartLibraryFiles.Save(Library(ReferenceParts.Delay(2)), folder);
            string path = Path.Combine(folder, "delay-2.json");
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"MinLatency\": 2", "\"MinLatency\": 0"));

            var refused = Assert.Throws<InvalidDataException>(() => PartLibraryFiles.Load(folder));

            Assert.Contains("delay-2.json", refused.Message);
            Assert.Contains("changes the contract", refused.Message);
        }

        [Fact]
        public void AMissingFolderLoadsAsAnEmptyLibrary() => Assert.Empty(PartLibraryFiles.Load(folder).Parts);
    }
}
