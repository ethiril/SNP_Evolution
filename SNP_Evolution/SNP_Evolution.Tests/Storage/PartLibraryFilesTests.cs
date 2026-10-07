using Newtonsoft.Json.Linq;
using SnpEvolution.Model;
using SnpEvolution.Search.Modules;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Verification;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.Fixtures.CompositionFixtures;

namespace SnpEvolution.Tests.Storage
{
    public sealed class PartLibraryFilesTests : IDisposable
    {
        private readonly TempFolder temp = new TempFolder("part-library");

        private string folder => temp.Path;

        public void Dispose() => temp.Dispose();

        private static readonly PartOrigin Evolved = new PartOrigin(7, "evolve-parts --seed 1", 1234);

        private static readonly PartOrigin Promoted = new PartOrigin(1, "a test", 0);

        private static Part FirstPartRegister() => ReferenceParts.Register() with { Contract = FirstParts.Named("register") };

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

            string network = JObject.Parse(json)["Network"]!.ToString();

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
            LibraryPart part = PartFixtures.Measured(delay, PartFixtures.ByHand) with { Proven = new ProvenBound(0, true, new StopReason(Stop.EveryInputChecked)) };

            LibraryPart read = PartLibraryFiles.Read(PartLibraryFiles.ToJson(part), "delay-2.json");

            Assert.Equal(part.Proven, read.Proven);
            Assert.Contains("\"Stopped\": \"every input checked\"", PartLibraryFiles.ToJson(part));
        }

        [Fact]
        [Slow]
        public void ALibraryFileWithACounterexampleIsRefused()
        {
            Part broken = PartFixtures.RegisterFailingAtTwenty();
            LibraryPart part = PartFixtures.Measured(broken, PartFixtures.ByHand) with { Proven = BoundedCheck.Prove(broken, new ProofLimits(TimeSpan.FromMinutes(1)), new EvaluationBudget()).Proven };

            var refusal = Assert.Throws<InvalidDataException>(() => PartLibraryFiles.Read(PartLibraryFiles.ToJson(part), "register.json"));

            Assert.Contains("n=20", refusal.Message);
        }

        // The file names its children and wiring, so a change to a child shows in the child's file alone.
        [Fact]
        public void APromotedPartIsStoredAsItsChildrenAndWiringRatherThanANetwork()
        {
            (ModuleLibrary library, Module increment) = Increments();
            (Composition chain, PortBinding binding) = ChainOfCopies(library, increment, 2);
            Promotion.Promote(chain, ArithmeticParts.AddTwo(), binding, library, Promoted, new EvaluationBudget(), _ => { });

            PartLibraryFiles.Save(library, folder);
            string file = File.ReadAllText(Path.Combine(folder, "add-2.json"));

            Assert.Contains("\"Recipe\"", file);
            Assert.DoesNotContain("\"Network\"", file);
            Assert.Contains("\"Contract\": \"increment\"", file);
            Assert.Contains("\"2.out\"", file);
        }

        // add 4 is two add 2s, each two increments, and its file sorts before the files it needs.
        [Fact]
        public void PartsBuiltFromPromotedPartsLoadAfterTheirChildren()
        {
            (ModuleLibrary library, Module increment) = Increments();
            (Composition two, PortBinding twoBinding) = ChainOfCopies(library, increment, 2);
            Module addTwo = Promotion.Promote(two, ArithmeticParts.AddTwo(), twoBinding, library, Promoted, new EvaluationBudget(), _ => { }).Module!;
            (Composition four, PortBinding fourBinding) = ChainOfCopies(library, addTwo, 2);
            Promotion.Promote(four, CatalogueEntry.Of(Specifications.AddConstant(4), FirstParts.Each(FirstParts.Values)).Contract, fourBinding, library, Promoted, new EvaluationBudget(), _ => { });

            PartLibraryFiles.Save(library, folder);
            ModuleLibrary loaded = PartLibraryFiles.Load(folder);

            Assert.Equal(new[] { "add 2", "add 4", "increment" }, loaded.Parts.Select(module => module.Part!.Contract.Name).Order());
            Assert.Equal(
                NetworkFiles.ToJson(library.PartFor("add 4")!.Part!.Part.Network),
                NetworkFiles.ToJson(loaded.PartFor("add 4")!.Part!.Part.Network));
        }

        [Fact]
        public void APromotedPartWhoseChildIsMissingIsRefusedByName()
        {
            (ModuleLibrary library, Module increment) = Increments();
            (Composition chain, PortBinding binding) = ChainOfCopies(library, increment, 2);
            Promotion.Promote(chain, ArithmeticParts.AddTwo(), binding, library, Promoted, new EvaluationBudget(), _ => { });
            PartLibraryFiles.Save(library, folder);
            File.Delete(Path.Combine(folder, "increment.json"));

            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => PartLibraryFiles.Load(folder));

            Assert.Contains("'add-2.json' is built from parts the folder does not have: increment", refused.Message);
        }
    }
}
