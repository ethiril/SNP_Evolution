using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;
using Xunit.Abstractions;
namespace SnpEvolution.Tests
{
    public class ZzStreamingScratch
    {
        private readonly ITestOutputHelper o; public ZzStreamingScratch(ITestOutputHelper o) => this.o = o;
        [Fact]
        public void Evolve()
        {
            string only = Environment.GetEnvironmentVariable("SCRATCH_TASK") ?? "Debounce";
            int M = int.Parse(Environment.GetEnvironmentVariable("SCRATCH_M") ?? "2"), W = int.Parse(Environment.GetEnvironmentVariable("SCRATCH_W") ?? "3");
            int cases = int.Parse(Environment.GetEnvironmentVariable("SCRATCH_CASES") ?? "4");
            BenchmarkTask task = new BenchmarkTask(only.StartsWith("Debounce") ? StreamingTask.Debouncer(M, W, cases) : StreamingTask.RateDetector(3, 6, cases), RuleForm.Standard, OutputTiming.Interval);
            var heldOut = only.StartsWith("Debounce") ? StreamingTask.Debouncer(M, W, 16, seed: 99) : StreamingTask.RateDetector(3, 6, 16, seed: 99);
            AlgorithmChoice algorithm = AlgorithmCatalog.All.First(a => a.Name.StartsWith("MAP-Elites"));
            var baseSettings = new SnpEvolution.Cli.Settings().BenchmarkSettings; var settings = baseSettings with { Space = baseSettings.Space with { MaxDelay = int.Parse(Environment.GetEnvironmentVariable("SCRATCH_DELAY") ?? "1") }, CreateEngine = () => Environment.GetEnvironmentVariable("SCRATCH_ENGINE") == "exact" ? new ExhaustiveCpuEngine(int.Parse(Environment.GetEnvironmentVariable("SCRATCH_CONFIGS") ?? "64")) : new ParallelCpuEngine(), Lexicase = true, Repetitions = int.Parse(Environment.GetEnvironmentVariable("SCRATCH_REPS") ?? "3"), PopulationSize = 40 };
            int seeds = int.Parse(Environment.GetEnvironmentVariable("SCRATCH_SEEDS") ?? "10");
            long budget = long.Parse(Environment.GetEnvironmentVariable("SCRATCH_BUDGET") ?? "30000");
            int solved = 0, general = 0;
            for (int seed = 1; seed <= seeds; seed++)
            {
                RunOutcome outcome = Benchmark.RunOnce(algorithm, task, seed, budget, settings);
                string line = $"[{DateTime.Now:HH:mm:ss}] seed {seed}: solved {outcome.Solved} evals {outcome.Evaluations} best {outcome.BestFitness:0.000}";
                if (outcome.Solved && outcome.Best != null)
                {
                    solved++;
                    var eval = new FitnessEvaluator(new ParallelCpuEngine(), heldOut, new SimulationOptions(0, 50, OutputTiming.Interval), 3, new Random(seed));
                    FitnessResult r = eval.Evaluate(outcome.Best.Genes);
                    line += $"  held-out {r.Fitness:0.000} ({r.Description})";
                    if (r.Fitness == 1) { general++; }
                    Directory.CreateDirectory(Environment.GetEnvironmentVariable("SCRATCH_OUT")!);
                    NetworkFiles.Save(outcome.Best.Genes, Path.Combine(Environment.GetEnvironmentVariable("SCRATCH_OUT")!, $"{only}-seed{seed}.json"));
                    line += "\n" + NetworkNotation.Format(outcome.Best.Genes);
                }
                if (!outcome.Solved && outcome.Best != null)
                {
                    var eval = new FitnessEvaluator(new ParallelCpuEngine(), task.Task, new SimulationOptions(0, 20, OutputTiming.Interval), 3, new Random(seed));
                    FitnessResult r = eval.Evaluate(outcome.Best.Genes);
                    line += "  failing: " + string.Join("; ", r.Checks!.Select((c, i) => (c, i)).Where(p => p.c < 1).Select(p => task.Task.CheckName(p.i) + $" {p.c:0.00}"));
                    var st = (StreamingTask)task.Task;
                    foreach (int ci in r.Checks!.Select((c, i) => (c, i)).Where(p => p.c < 1).Select(p => p.i).Take(1))
                    {
                        string name = task.Task.CheckName(ci);
                        int caseIdx = int.Parse(name.Split(' ')[1]) - 1;
                        line += $"\n   case input: {string.Join(",", st.Streams[caseIdx].InputSteps.OrderBy(x => x))}";
                        var res = new SequentialCpuEngine().Run(new[] { new Trial(outcome.Best.Genes, st.Cases[caseIdx].Input, Readout.SpikeTrain) }, new SimulationOptions(st.StepsNeeded, 1, OutputTiming.Interval), new Random(1))[0];
                        line += $"\n   output:     {string.Join(",", res.SpikeTrains[0])}";
                    }
                    line += "\n" + NetworkNotation.Format(outcome.Best.Genes);
                }
                o.WriteLine(line);
            }
            o.WriteLine($"solved {solved}/{seeds}, held-out perfect {general}");
        }
    }
}
