using SnpEvolution.Cli;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Cli
{
    public sealed class PartsSessionTests : IDisposable
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

        private PartsSession.Options Options(string library, bool redo = false) =>
            new PartsSession.Options(Seed: 1, Budget: 4000, Delays, Path.Combine(folder, library), () => new ExhaustiveCpuEngine(), redo);

        private static Dictionary<string, string> Files(string library) =>
            Directory.GetFiles(library).ToDictionary(path => Path.GetFileName(path), File.ReadAllText);

        [Fact]
        public void TheSameSeedWritesTheSameLibrary()
        {
            var log = new List<string>();

            Assert.Equal(0, PartsSession.Run(Options("first"), log.Add));
            Assert.Equal(0, PartsSession.Run(Options("second"), _ => { }));

            Dictionary<string, string> first = Files(Path.Combine(folder, "first"));
            Assert.Equal(new[] { "delay-2.json", "delay-4.json" }, first.Keys.Order());
            Assert.Equal(first, Files(Path.Combine(folder, "second")));
            Assert.Contains(log, line => line.StartsWith("Contract") && line.Contains("Evaluations") && line.Contains("Latency"));
        }

        [Fact]
        public void TheLibraryReloadsWhatTheRunSaved()
        {
            PartsSession.Run(Options("library"), _ => { });

            var loaded = PartLibraryFiles.Load(Path.Combine(folder, "library")).Parts.Select(module => module.Part!).ToList();

            Assert.Equal(new[] { "delay 2", "delay 4" }, loaded.Select(part => part.Contract.Name).Order());
            Assert.All(loaded, part => Assert.True(part.Proven?.AllInputs));
        }

        [Fact]
        public void ContractsAlreadySolvedAreKeptUnlessRedone()
        {
            PartsSession.Run(Options("library"), _ => { });
            var log = new List<string>();

            PartsSession.Run(Options("library"), log.Add);
            Assert.DoesNotContain(log, line => line.StartsWith("Evolving"));

            PartsSession.Run(Options("library", redo: true), log.Add);
            Assert.Contains(log, line => line == "Evolving a part for delay 2.");
        }

        [Fact]
        public void ABrokenLibraryIsReportedAndNothingIsEvolved()
        {
            string library = Path.Combine(folder, "broken");
            Directory.CreateDirectory(library);
            File.WriteAllText(Path.Combine(library, "delay-2.json"), "{ not json");
            var log = new List<string>();

            Assert.Equal(1, PartsSession.Run(Options("broken"), log.Add));
            Assert.Contains(log, line => line.Contains("delay-2.json"));
        }

        [Fact]
        public void TheSummaryTableHasAColumnPerReportedMeasure()
        {
            string table = PartsSession.FormatTable(new[] { new PartsSession.Row("add", "not solved", 50_000, null) });

            Assert.StartsWith("Contract   Result       Evaluations   Neurons   Synapses   Latency   Robust j=1   Robust j=2", table);
            Assert.Contains("add        not solved   50000         -         -          -         -            -", table);
        }
    }
}
