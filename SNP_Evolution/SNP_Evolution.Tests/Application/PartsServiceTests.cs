using SnpEvolution.Application;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Application
{
    public sealed class PartsServiceTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "parts-session-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private static readonly IReadOnlyList<Contract> Delays = new[] { FirstParts.Named("delay 2"), FirstParts.Named("delay 4") };

        private Settings Library(string library) => new Settings { PartBudget = 4000, PartLibraryFolder = Path.Combine(folder, library) };

        private static PartsRequest Request(bool redo = false) => new PartsRequest(Delays, Seed: 1, Redo: redo);

        private int Run(string library, Action<string> log, bool redo = false)
        {
            PartsResult result = PartsService.Run(Library(library), Request(redo), log);
            return result.Error != null ? 1 : result.AllSolved ? 0 : 2;
        }

        private static Dictionary<string, string> Files(string library) =>
            Directory.GetFiles(library).ToDictionary(path => Path.GetFileName(path), File.ReadAllText);

        [Fact]
        [Slow]
        public void TheSameSeedWritesTheSameLibrary()
        {
            var log = new List<string>();

            Assert.Equal(0, Run("first", log.Add));
            Assert.Equal(0, Run("second", _ => { }));

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
        public void ABrokenLibraryIsReportedAndNothingIsEvolved()
        {
            string library = Path.Combine(folder, "broken");
            Directory.CreateDirectory(library);
            File.WriteAllText(Path.Combine(library, "delay-2.json"), "{ not json");
            var log = new List<string>();

            Assert.Equal(1, Run("broken", log.Add));
            Assert.Contains(log, line => line.Contains("delay-2.json"));
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
