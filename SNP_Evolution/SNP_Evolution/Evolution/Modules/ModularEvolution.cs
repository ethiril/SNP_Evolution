using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
{
    // How the modular loop reacts: Patience is the generations without improvement before it does, SideGenerations
    // the most a side run gets, and CompositeFraction the share of the population that gets a new module copy.
    public sealed record ModulePolicy(int Patience = 25, int SideGenerations = 60, double CompositeFraction = 0.25);

    // Builds a run up from parts. Mutation already copies library modules into networks, and the tracker keeps the
    // changes that pay off as new modules. On top of that this keeps every network that solves a stage as a module,
    // and when the search stalls it reads the task's checks to find the first part no network does yet, evolves a
    // network for just that part on the side, keeps it as a module, and puts copies of it into the best networks so
    // the main search can learn to wire it in.
    public sealed class ModularEvolution : IGeneticAlgorithm
    {
        private const float ImprovementTolerance = 1e-6f;

        private readonly IGeneticAlgorithm inner;
        private readonly ModuleLibrary library;
        private readonly ModuleTracker? tracker;
        private readonly Func<ITask> currentTask;
        private readonly Func<ITask, IGeneticAlgorithm> createSideRun;
        private readonly ModulePolicy policy;
        private readonly int populationSize;
        private readonly int maxNeurons;
        private readonly Random random;
        private readonly Action<string> log;
        private float? bestFitness;
        private int stale;

        // currentTask gives the task the run is scored on now; createSideRun builds an algorithm for a smaller one.
        public ModularEvolution(
            IGeneticAlgorithm inner,
            ModuleLibrary library,
            ModuleTracker? tracker,
            Func<ITask> currentTask,
            Func<ITask, IGeneticAlgorithm> createSideRun,
            ModulePolicy policy,
            int populationSize,
            int maxNeurons,
            Random random,
            Action<string>? log = null)
        {
            this.inner = inner;
            this.library = library;
            this.tracker = tracker;
            this.currentTask = currentTask;
            this.createSideRun = createSideRun;
            this.policy = policy;
            this.populationSize = populationSize;
            this.maxNeurons = maxNeurons;
            this.random = random;
            this.log = log ?? Console.WriteLine;
        }

        public IGeneticAlgorithm Inner => inner;

        public ModuleLibrary Library => library;

        public int SideRuns { get; private set; }

        public IReadOnlyList<Individual> Population => inner.Population;

        public int Generation => inner.Generation;

        public Individual? Best => inner.Best;

        public IReadOnlyList<IReadOnlyList<float>> FitnessHistory => inner.FitnessHistory;

        public void NextGeneration()
        {
            inner.NextGeneration();
            if (inner.Best is not Individual best || !GeneticAlgorithm.IsRecordableFitness(best.Fitness))
            {
                return;
            }
            if (bestFitness == null || best.Fitness > bestFitness + ImprovementTolerance)
            {
                bestFitness = best.Fitness;
                stale = 0;
                return;
            }
            if (++stale < policy.Patience)
            {
                return;
            }
            stale = 0;
            React();
        }

        public void Immigrate(IReadOnlyList<Network> newcomers) => inner.Immigrate(newcomers);

        // Called when the task changes, which in an iterative run means the stage was solved, so the best network
        // does the whole of the old task and is worth keeping as a part.
        public void Rescore()
        {
            if (inner.Best is Individual best && FitnessEvaluator.IsSolvingFitness(best.Fitness))
            {
                library.Add(ModuleCuts.Whole(best.Genes), $"the network that solved {currentTask().Name}");
            }
            inner.Rescore();
            tracker?.Reset();
            bestFitness = null;
            stale = 0;
        }

        private void React()
        {
            ITask task = currentTask();
            CheckDiagnosis diagnosis = CheckDiagnosis.Of(inner.Population);
            log($"No improvement in {policy.Patience} generations. {diagnosis.Describe(task)}");
            Module? built = diagnosis.Frontier is int frontier && task.Focus(frontier) is ITask focus ? SideRun(focus) : null;
            List<Network> composites = Composites(built);
            if (composites.Count > 0)
            {
                inner.Immigrate(composites);
                log($"{composites.Count} of the best networks get a copy of {(built != null ? $"module {built.Id}" : "a library module")} to wire in.");
            }
        }

        // Evolves a network for the focus task alone, and keeps the best one found as a module.
        private Module? SideRun(ITask focus)
        {
            SideRuns++;
            IGeneticAlgorithm side = createSideRun(focus);
            int generation = 0;
            while (generation < policy.SideGenerations && !(side.Best is Individual solved && FitnessEvaluator.IsSolvingFitness(solved.Fitness)))
            {
                side.NextGeneration();
                generation++;
            }
            if (side.Best is not Individual best || best.Fitness <= 0)
            {
                log($"Side run for {focus.Name} found nothing in {generation} generations.");
                return null;
            }
            log($"Side run for {focus.Name}: fitness {best.Fitness:0.000} in {generation} generations ({best.Description}).");
            return library.Add(ModuleCuts.Whole(best.Genes), $"a side run for {focus.Name}, fitness {best.Fitness:0.000}");
        }

        // Copies of the module, or of library modules, put into the best networks.
        private List<Network> Composites(Module? module)
        {
            int count = Math.Max(1, (int)Math.Round(populationSize * policy.CompositeFraction));
            List<Individual> best = Ranking.Rank(inner.Population.Where(individual => individual.IsEvaluated)).Take(Math.Max(1, count / 2)).ToList();
            var composites = new List<Network>();
            for (int attempt = 0; best.Count > 0 && attempt < 3 * count && composites.Count < count; attempt++)
            {
                Network host = best[attempt % best.Count].Genes;
                if ((module ?? library.Choose(random)) is not Module chosen)
                {
                    break;
                }
                Network composite = ModuleEdits.Insert(host, chosen, library.NextInstance(), maxNeurons, random);
                if (composite != host)
                {
                    composites.Add(composite);
                }
            }
            return composites;
        }
    }
}
