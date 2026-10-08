using SnpEvolution.Application;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Application
{
    public sealed class PartsServiceTests : IDisposable
    {
        private static readonly IReadOnlyList<Contract> Delays = new[] { FirstParts.Named("delay 2"), FirstParts.Named("delay 4") };

        private readonly TempFolder temp = new TempFolder("parts-session");

        private string folder => temp.Path;

        public void Dispose() => temp.Dispose();

        private Settings Library(string library) => new Settings { PartBudget = 4000, PartLibraryFolder = Path.Combine(folder, library) };

        private static PartsRequest Request(bool redo = false) => new PartsRequest(Delays, Seed: 1, Redo: redo);

        private PartsResult Run(string library, Action<string> log, bool redo = false) =>
            PartsService.Run(Library(library), Request(redo), log);

        private static Dictionary<string, string> Files(string library) =>
            Directory.GetFiles(library).ToDictionary(path => Path.GetFileName(path), File.ReadAllText);

        [Fact]
        public void AfterAnEarlyStopNoMoreContractsAreEvolvedAndTheRunStillEnds()
        {
            var log = new List<string>();
            PartsResult result;
            using (EarlyStop stop = EarlyStop.Begin())
            {
                stop.Request();
                result = Run("stopped", log.Add);
            }

            Assert.Null(result.Error);
            Assert.Empty(result.Rows);
            Assert.Contains("Stopped early: the contracts left are not evolved.", log);
            Assert.Contains(log, line => line.StartsWith("Saved 0 part(s)"));
        }

        [Fact]
        [Slow]
        public void TheSameSeedWritesTheSameLibrary()
        {
            var log = new List<string>();

            PartsResult firstRun = Run("first", log.Add);
            PartsResult secondRun = Run("second", _ => { });

            Assert.Null(firstRun.Error);
            Assert.True(firstRun.AllSolved);
            Assert.Null(secondRun.Error);
            Assert.True(secondRun.AllSolved);

            Dictionary<string, string> first = Files(Path.Combine(folder, "first"));
            Assert.Equal(new[] { "delay-2.json", "delay-4.json" }, first.Keys.Order());
            Assert.Equal(first, Files(Path.Combine(folder, "second")));
            Assert.Contains(log, line => line.StartsWith("Contract") && line.Contains("Evaluations") && line.Contains("Latency"));
        }

        [Fact]
        [Slow]
        public void TheLibraryReloadsWhatTheRunSaved()
        {
            Run("library", _ => { });

            var loaded = PartLibraryFiles.Load(Path.Combine(folder, "library")).Parts.Select(module => module.Part!).ToList();

            Assert.Equal(new[] { "delay 2", "delay 4" }, loaded.Select(part => part.Contract.Name).Order());
            Assert.All(loaded, part => Assert.True(part.Proven?.AllInputs));
        }

        [Fact]
        [Slow]
        public void ContractsAlreadySolvedAreKeptUnlessRedone()
        {
            Run("library", _ => { });
            var log = new List<string>();

            Run("library", log.Add);
            Assert.DoesNotContain(log, line => line.StartsWith("Evolving"));

            Run("library", log.Add, redo: true);
            Assert.Contains(log, line => line == "Evolving a part for delay 2.");
        }

        [Fact]
        public void CompilingOnlyNeverSearchesForAPartTheCompilerCannotMake()
        {
            var log = new List<string>();
            Settings underProfile = Library("compile-only");
            underProfile.HardwareProfile = true;

            PartsResult noCounts = PartsService.Run(Library("compile-only"), new PartsRequest(Delays, Seed: 1, Route: PartRoute.Compile), log.Add);
            PartsResult profiled = PartsService.Run(underProfile, new PartsRequest(new[] { FirstParts.Named("register") }, Seed: 1, Route: PartRoute.Compile), log.Add);

            Assert.Equal(new[] { "not compilable", "not compilable", "not compilable" }, noCounts.Rows.Concat(profiled.Rows).Select(row => row.Status));
            Assert.Contains("delay 2: not compiled, since it has no count ports.", log);
            Assert.Contains("register: not compiled, since the compiler's modules are outside the hardware profile.", log);
            Assert.DoesNotContain(log, line => line.StartsWith("Evolving") || line.StartsWith("Compiling"));
        }

        [Fact]
        [Slow]
        public void BothRoutesKeepACompiledPartWithoutSearching()
        {
            var log = new List<string>();

            PartsResult result = PartsService.Run(Library("both"), new PartsRequest(new[] { FirstParts.Named("register") }, Seed: 1), log.Add);

            PartsService.Row row = Assert.Single(result.Rows);
            Assert.Equal("solved", row.Status);
            Assert.NotNull(row.Part?.Origin.Program);
            Assert.DoesNotContain(log, line => line.StartsWith("Evolving"));
        }

        [Fact]
        public void ABrokenLibraryIsReportedAndNothingIsEvolved()
        {
            string library = Path.Combine(folder, "broken");
            Directory.CreateDirectory(library);
            PartFixtures.WriteBrokenLibrary(library);

            PartsResult result = PartsService.Run(Library("broken"), Request(), _ => { });

            Assert.Contains("delay-2.json", result.Error);
            Assert.Empty(result.Rows);
        }

        [Fact]
        public void TheSummaryTableHasAColumnPerReportedMeasure()
        {
            string table = PartsService.FormatTable(new[] { new PartsService.Row("add", "not solved", 50_000, null) });

            Assert.StartsWith("Contract   Result       Evaluations   Neurons   Synapses   Latency   Robust j=1   Robust j=2", table);
            Assert.Contains("add        not solved   50000         -         -          -         -            -", table);
        }
    }
}
