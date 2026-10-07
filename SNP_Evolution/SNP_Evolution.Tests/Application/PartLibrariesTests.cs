using SnpEvolution.Application;
using SnpEvolution.Evolution.Parts;

namespace SnpEvolution.Tests.Application
{
    public class PartLibrariesTests
    {
        [Fact]
        [Slow]
        public void HandBuiltLeavesLeaveOutThePromotedAddLoop()
        {
            string missing = Path.Combine(Path.GetTempPath(), "snp-no-parts-" + Guid.NewGuid().ToString("N"));

            ModuleLibrary leaves = PartLibraries.Load(missing, handBuilt: true, addLoop: false).Value!;
            ModuleLibrary withLoop = PartLibraries.Load(missing, handBuilt: true, addLoop: true).Value!;

            Assert.NotNull(leaves.PartFor("gate"));
            Assert.Null(leaves.PartFor("add loop"));
            Assert.NotNull(withLoop.PartFor("add loop"));
        }

        [Fact]
        public void ABrokenLibraryIsATypedErrorNamingTheFile()
        {
            string folder = Path.Combine(Path.GetTempPath(), "snp-broken-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(Path.Combine(folder, "delay-2.json"), "{ not json");

                Loaded<ModuleLibrary> loaded = PartLibraries.Load(folder, handBuilt: false, addLoop: false);

                Assert.Null(loaded.Value);
                Assert.Contains("delay-2.json", loaded.Error);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
