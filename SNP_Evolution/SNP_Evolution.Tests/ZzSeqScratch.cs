using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;
using Xunit.Abstractions;
namespace SnpEvolution.Tests
{
    public class ZzSeqScratch
    {
        private readonly ITestOutputHelper o; public ZzSeqScratch(ITestOutputHelper o) => this.o = o;
        [Fact]
        public void Check()
        {
            var lib = PartLibraryFiles.Load("../../../../../parts", _ => { });
            Part part = lib.PartFor("sequencer 2")!.Part!.Part;
            ContractTask task = part.Task();
            o.WriteLine(NetworkNotation.Format(part.Network));
            foreach (int j in new[] { 0, 1 })
            {
                var trials = task.Cases.Select(c => new Trial(part.Network, c.Input, c.Readout, c.Watch! with { StepsAfterDone = c.Watch.StepsAfterDone * (1 + j) })).ToList();
                var results = new ParallelCpuEngine().Run(trials, new SimulationOptions(task.StepsNeeded * (1 + j), 10, OutputTiming.Interval, j), new Random(1));
                o.WriteLine($"j={j}: {task.Describe(results)}");
                foreach (var run in results[0].PortRuns.Take(3)) o.WriteLine("  " + task.Reading(run, 0) + "  final " + string.Join(",", run.FinalSpikes) + " firings " + string.Join(" | ", run.Firings.Select(f => string.Join(",", f.Select(x => x.Step)))));
            }
        }
    }
}
