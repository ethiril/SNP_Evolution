using SnpEvolution.Simulation;
using SnpEvolution.Storage;
using Xunit.Abstractions;
namespace SnpEvolution.Tests
{
    public class ScratchCheck
    {
        private readonly ITestOutputHelper o; public ScratchCheck(ITestOutputHelper o) => this.o = o;
        [Fact]
        public void Check()
        {
            foreach (string name in new[] { "delay-2", "delay-3", "delay-4", "sequencer-2" })
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText($"../../../../../parts/{name}.json"));
                var net = NetworkFiles.FromJson(json["Network"]!.ToString())!;
                var pos = json["Binding"]!["Positions"]!.ToObject<Dictionary<string, int>>()!;
                foreach (int start in new[] { 2, 6, 10 })
                {
                    var watch = new PortWatch(pos.Values.ToList(), new[] { pos["done"] }, 3);
                    var trial = new Trial(net, new InputSpikes(new[] { (IReadOnlyList<int>)new[] { start } }), Readout.Ports, watch);
                    var r = new ExhaustiveCpuEngine().Run(new[] { trial }, new SimulationOptions(30, 3), new Random(0))[0];
                    foreach (var run in r.PortRuns)
                        o.WriteLine($"{name} start sent step {start}: " + string.Join("  ", pos.Keys.Select((k, i) => $"{k}@[{string.Join(",", run.Firings[i].Select(f => f.Step))}]")));
                }
            }
        }
    }
}
