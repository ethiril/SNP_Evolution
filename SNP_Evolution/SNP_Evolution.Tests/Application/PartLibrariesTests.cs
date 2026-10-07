using SnpEvolution.Application;
using SnpEvolution.Specs.Parts;

namespace SnpEvolution.Tests.Application
{
    public class PartLibrariesTests
    {
        [Fact]
        [Slow]
        public void HandBuiltLeavesLeaveOutThePromotedAddLoop()
        {
            using var temp = new TempFolder("snp-no-parts");
            string missing = temp.Path;

            ModuleLibrary leaves = PartLibraries.Load(missing, handBuilt: true, addLoop: false).Value!;
            ModuleLibrary withLoop = PartLibraries.Load(missing, handBuilt: true, addLoop: true).Value!;

            Assert.NotNull(leaves.PartFor("gate"));
            Assert.Null(leaves.PartFor("add loop"));
            Assert.NotNull(withLoop.PartFor("add loop"));
        }

        [Fact]
        public void ABrokenLibraryIsATypedErrorNamingTheFile()
        {
            using var temp = new TempFolder("snp-broken").Made();
            string folder = temp.Path;
            File.WriteAllText(Path.Combine(folder, "delay-2.json"), "{ not json");

            Loaded<ModuleLibrary> loaded = PartLibraries.Load(folder, handBuilt: false, addLoop: false);

            Assert.Null(loaded.Value);
            Assert.Contains("delay-2.json", loaded.Error);
        }
    }
}
