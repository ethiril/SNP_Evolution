using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Storage;

namespace SnpEvolution.Cli
{
    // A run's reach is how many of the target's checks its best network gets right, in order, before the first it misses.
    internal static class ReachSession
    {
        public sealed record Setup(string Name, string Description, Action<Settings> Apply);

        public sealed record Outcome(string Setup, int Seed, int Reach, int Neurons, long Evaluations, long UpFront, double Seconds);

        public static readonly IReadOnlyList<Setup> Setups = new[]
        {
            new Setup("flat", "MAP-Elites over network size, no modules", settings =>
            {
                settings.Algorithm = Catalog.StructuralDefault;
                settings.Modules = false;
            }),
            new Setup("modules", "MAP-Elites with the modular loop and lexicase parents", settings =>
            {
                settings.Algorithm = Catalog.StructuralDefault;
                settings.Modules = true;
                settings.Lexicase = true;
            }),
            new Setup("composition", "composition search with MAP-Elites, from the part library", settings =>
            {
                settings.Algorithm = SearchCatalog.CompositionMapElites;
                settings.Modules = false;
            }),
        };

        // 0 when every run finished. Runs go in parallel, each on one CPU thread, so wall times are comparable.
        public static int Run(Settings settings, IReadOnlyList<Setup> setups, int seeds, bool chargeParts, string command, Action<string> log)
        {
            if (settings.MaxEvaluations <= 0)
            {
                log("reach needs an evaluation budget (--evaluations N), since the setups are compared on it.");
                return 1;
            }
            long partCost = PartLibraryFiles.Load(settings.PartLibraryFolder).PartEvaluations;
            var jobs = (from setup in setups from seed in Enumerable.Range(1, seeds) select (setup, seed)).ToList();
            var outcomes = new Outcome[jobs.Count];
            int finished = 0;
            Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, index =>
            {
                (Setup setup, int seed) = jobs[index];
                outcomes[index] = RunOnce(settings, setup, seed, chargeParts ? partCost : 0);
                log($"[{Interlocked.Increment(ref finished)}/{jobs.Count}] {setup.Name}, seed {seed}: reach {outcomes[index].Reach}, " +
                    $"{outcomes[index].Neurons} neurons, {outcomes[index].Evaluations} evaluations, {outcomes[index].Seconds:0}s");
            });
            string report = Report(settings, setups, outcomes, partCost, chargeParts, command);
            log("");
            log(report);
            string folder = EvolutionSession.NewOutputFolder();
            Directory.CreateDirectory(folder);
            NetworkFiles.SaveText(report, Path.Combine(folder, "reach.md"));
            NetworkFiles.SaveText(Csv(outcomes), Path.Combine(folder, "reach.csv"));
            log($"Saved to {folder}");
            return 0;
        }

        // With the parts charged, composition search's budget is cut by what they cost so every setup spends the same in all.
        internal static Outcome RunOnce(Settings settings, Setup setup, int seed, long partCost)
        {
            Settings own = settings.Copy();
            setup.Apply(own);
            own.Engine = Catalog.Engines.Single(engine => engine.Name == "CPU, single thread");
            bool composition = own.Algorithm is CompositionSearch;
            own.MaxEvaluations = Math.Max(1, settings.MaxEvaluations - (composition ? partCost : 0));
            BenchmarkTask task = own.SelectedTask;
            EvaluationBudget evaluations = own.RunBudget();
            var random = new Random(seed);
            var clock = Stopwatch.StartNew();
            IGeneticAlgorithm run = EvolutionSession.Evolve(own, task, factory => factory.NewNetwork(), random, _ => { }, evaluations);
            clock.Stop();
            if (run.Best is not Individual best)
            {
                return new Outcome(setup.Name, seed, 0, 0, evaluations.Networks, evaluations.UpFront, clock.Elapsed.TotalSeconds);
            }
            var scorer = new FitnessEvaluator(own.Engine.Create(own), task.Task, own.SimulationOptions with { Timing = task.Timing }, 1, new Random(seed), new EvaluationBudget());
            int reach = Reach(scorer.Evaluate(best.Genes).Checks ?? Array.Empty<float>());
            return new Outcome(setup.Name, seed, reach, best.Genes.Neurons.Count, evaluations.Networks, evaluations.UpFront, clock.Elapsed.TotalSeconds);
        }

        // A check adds up a share per sampled run, so one every run gets right can fall short of 1 by rounding.
        internal static int Reach(IEnumerable<float> checks) => checks.TakeWhile(check => check >= 1 - 1e-4f).Count();

        private static string Report(Settings settings, IReadOnlyList<Setup> setups, IReadOnlyList<Outcome> outcomes, long partCost, bool chargeParts, string command)
        {
            var text = new StringBuilder();
            text.AppendLine($"Target {settings.Target}, {outcomes.Count(outcome => outcome.Setup == setups[0].Name)} seeds per setup, " +
                $"{settings.MaxEvaluations.ToString("N0", CultureInfo.InvariantCulture)} evaluations per run. The part library cost " +
                $"{partCost.ToString("N0", CultureInfo.InvariantCulture)} evaluations to evolve" +
                (chargeParts ? ", taken off composition search's budget." : ", not charged to any run."));
            text.AppendLine();
            text.AppendLine("| Setup | Median reach | Mean reach | Min-max | Median neurons | Median evaluations | Median wall time (s) |");
            text.AppendLine("|---|---|---|---|---|---|---|");
            foreach (Setup setup in setups)
            {
                List<Outcome> own = outcomes.Where(outcome => outcome.Setup == setup.Name).ToList();
                text.AppendLine($"| {setup.Name} ({setup.Description}) | {Statistics.Median(own.Select(outcome => (double)outcome.Reach)):0.#} | " +
                    $"{own.Average(outcome => outcome.Reach):0.0} | {own.Min(outcome => outcome.Reach)}-{own.Max(outcome => outcome.Reach)} | " +
                    $"{Statistics.Median(own.Select(outcome => (double)outcome.Neurons)):0} | {Statistics.Median(own.Select(outcome => (double)outcome.Evaluations)):0} | " +
                    $"{Statistics.Median(own.Select(outcome => outcome.Seconds)):0} |");
            }
            if (setups.Count > 1)
            {
                text.AppendLine();
                text.AppendLine("| Comparison of reach | U | p (two-sided) | A12 |");
                text.AppendLine("|---|---|---|---|");
                double[] Reaches(Setup setup) => outcomes.Where(outcome => outcome.Setup == setup.Name).Select(outcome => (double)outcome.Reach).ToArray();
                for (int first = 0; first < setups.Count; first++)
                {
                    for (int second = first + 1; second < setups.Count; second++)
                    {
                        MannWhitneyResult test = Statistics.MannWhitney(Reaches(setups[second]), Reaches(setups[first]));
                        text.AppendLine($"| {setups[second].Name} vs {setups[first].Name} | {test.U:0.#} | {test.P:0.####}{(test.Exact ? "" : " (normal approx.)")} | {test.A12:0.00} |");
                    }
                }
                text.AppendLine();
                text.AppendLine("A12 is the chance a run of the first setup reaches further than one of the second; 0.5 is no difference.");
            }
            text.AppendLine();
            text.AppendLine("Command: `" + command + "`");
            return text.ToString();
        }

        private static string Csv(IEnumerable<Outcome> outcomes)
        {
            var text = new StringBuilder("setup,seed,reach,neurons,evaluations,up_front,seconds\n");
            foreach (Outcome outcome in outcomes)
            {
                text.AppendLine(string.Join(",", outcome.Setup, outcome.Seed, outcome.Reach, outcome.Neurons, outcome.Evaluations, outcome.UpFront,
                    outcome.Seconds.ToString("0.0", CultureInfo.InvariantCulture)));
            }
            return text.ToString();
        }
    }
}
