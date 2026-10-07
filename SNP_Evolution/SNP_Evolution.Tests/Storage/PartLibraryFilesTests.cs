using SnpEvolution.Model;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Storage
{
    public sealed class PartLibraryFilesTests : IDisposable
    {
        private readonly TempFolder temp = new TempFolder("part-library");

        private string folder => temp.Path;

        public void Dispose() => temp.Dispose();

        private static readonly PartOrigin Evolved = new PartOrigin(7, "evolve-parts --seed 1", 1234);

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
                library.AddPart(PartFixtures.Measured(part, Evolved), "a test");
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
            string json = PartLibraryFiles.ToJson(PartFixtures.Measured(delay, Evolved));

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

        [Fact]
        public void ALibraryFileKeepsItsProvenBound()
        {
            Part delay = ReferenceParts.Delay(2);
            LibraryPart part = PartFixtures.Measured(delay, new PartOrigin(0, "by hand", 0)) with { Proven = new ProvenBound(0, true, new StopReason(Stop.EveryInputChecked)) };

            LibraryPart read = PartLibraryFiles.Read(PartLibraryFiles.ToJson(part), "delay-2.json");

            Assert.Equal(part.Proven, read.Proven);
            Assert.Contains("\"Stopped\": \"every input checked\"", PartLibraryFiles.ToJson(part));
        }
    }
}
