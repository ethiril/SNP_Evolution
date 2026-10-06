using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
{
    // How the modular loop reacts: Patience is the generations without improvement before it does, SideGenerations
    // the most a side run gets, and CompositeFraction the share of the population that gets a new module copy.
    // Triggered lets every other side run build a part that starts on a spike from the host, where the task has
    // one. IncubationGenerations is how long the networks given a module copy evolve on their own before they join
    // the main run; 0 sends them in at once.
    public sealed record ModulePolicy(int Patience = 25, int SideGenerations = 60, double CompositeFraction = 0.25, bool Triggered = true, int IncubationGenerations = 30);

    // Builds a run up from parts. Mutation already copies library modules into networks, and the tracker keeps the
    // changes that pay off as new modules. On top of that this keeps every network that solves a stage as a module,
    // and when the search stalls it reads the task's checks to find the first part no network does yet, evolves a
    // network for just that part on the side, and keeps it as a module. That part either runs from the first step,
    // or, every other time, waits for a trigger so it can follow on from the host. Copies of it go into the best
    // networks, which then evolve apart for a while, seeded only with those copies, so wiring the part in is not
    // judged against hosts that have had all run to settle; the best of them then join the main run. A part that
    // solved its side run is reused rather than evolved again.
    public sealed class ModularEvolution : IGeneticAlgorithm
    {
        private const float ImprovementTolerance = 1e-6f;

        // How much an incubated network must beat the main run by to count, more than sampled runs vary by when the
        // same network is scored again.
        private const float IncubationMargin = 1e-3f;

        private readonly IGeneticAlgorithm inner;
        private readonly ModuleLibrary library;
        private readonly ModuleTracker? tracker;
        private readonly Func<ITask> currentTask;
        private readonly Func<ITask, IReadOnlyList<Network>?, IGeneticAlgorithm> createSideRun;
        private readonly Dictionary<string, int> solvedParts = new Dictionary<string, int>();
        private readonly ModulePolicy policy;
        private readonly int populationSize;
        private readonly int maxNeurons;
        private readonly Random random;
        private readonly Action<string> log;
        private float? bestFitness;
        private int stale;
        private int builds;

        // currentTask gives the task the run is scored on now. createSideRun builds an algorithm for another task,
        // starting from random networks, or from copies of the given seeds when there are some.
        public ModularEvolution(
            IGeneticAlgorithm inner,
            ModuleLibrary library,
            ModuleTracker? tracker,
            Func<ITask> currentTask,
            Func<ITask, IReadOnlyList<Network>?, IGeneticAlgorithm> createSideRun,
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

        // Generations run on the side, for parts and incubation, which cost evaluations the main run does not count.
        public int SideGenerationsRun { get; private set; }

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
            Module? built = diagnosis.Frontier is int frontier && Part(task, frontier) is ITask part ? Build(part) : null;
            List<Network> composites = Composites(built);
            if (composites.Count == 0)
            {
                return;
            }
            string given = built != null ? $"module {built.Id}" : "a library module";
            if (policy.IncubationGenerations > 0)
            {
                composites = Incubate(task, composites, built);
                log($"{composites.Count} network(s) built around {given} join the run.");
            }
            else
            {
                log($"{composites.Count} of the best networks get a copy of {given} to wire in.");
            }
            inner.Immigrate(composites);
        }

        // The part to build for a check: every other time one that waits for a trigger, where the task has one.
        private ITask? Part(ITask task, int check)
        {
            builds++;
            ITask? triggered = policy.Triggered && builds % 2 == 0 ? task.Triggered(check) : null;
            return triggered ?? task.Focus(check);
        }

        // The module for the part: the one kept when it was solved before, or the best network of a new side run.
        private Module? Build(ITask part)
        {
            if (solvedParts.TryGetValue(part.Name, out int id) && library.Find(id) is Module known)
            {
                log($"Module {id} already does {part.Name}.");
                return known;
            }
            (Module? built, bool solved) = SideRun(part);
            if (built != null && solved)
            {
                solvedParts[part.Name] = built.Id;
            }
            return built;
        }

        // Evolves a network for the part alone, keeps the best one found as a module, and says whether it solved it.
        private (Module?, bool) SideRun(ITask part)
        {
            SideRuns++;
            IGeneticAlgorithm side = createSideRun(part, null);
            int generation = Evolve(side, policy.SideGenerations, best => FitnessEvaluator.IsSolvingFitness(best.Fitness));
            if (side.Best is not Individual best || best.Fitness <= 0)
            {
                log($"Side run for {part.Name} found nothing in {generation} generations.");
                return (null, false);
            }
            log($"Side run for {part.Name}: fitness {best.Fitness:0.000} in {generation} generations ({best.Description}).");
            return (library.Add(ModuleCuts.Whole(best.Genes), $"a side run for {part.Name}, fitness {best.Fitness:0.000}"), FitnessEvaluator.IsSolvingFitness(best.Fitness));
        }

        // Evolves the networks given a module copy apart from the main run, stopping early once one beats the main
        // run's best, and returns as many of its best networks as were given. A module that was built for this
        // stall is credited with whether it led to a better network.
        private List<Network> Incubate(ITask task, List<Network> composites, Module? module)
        {
            float target = bestFitness ?? float.MinValue;
            IGeneticAlgorithm nursery = createSideRun(task, composites);
            int generation = Evolve(nursery, policy.IncubationGenerations, best => best.Fitness > target + IncubationMargin);
            List<Individual> ranked = Ranking.Rank(nursery.Population.Where(individual => individual.IsEvaluated));
            if (ranked.Count == 0)
            {
                return composites;
            }
            bool better = ranked[0].Fitness > target + IncubationMargin;
            if (module != null)
            {
                library.Credit(module.Id, better);
            }
            log($"Incubated {composites.Count} network(s) with {(module != null ? $"module {module.Id}" : "library modules")} for {generation} generations: " +
                $"best {ranked[0].Fitness:0.000} against {target:0.000} in the main run{(better ? ", an improvement" : "")}.");
            return ranked.Take(composites.Count).Select(individual => individual.Genes).ToList();
        }

        // Runs the algorithm until done says its best is good enough or the generations run out, and returns how
        // many it ran.
        private int Evolve(IGeneticAlgorithm algorithm, int generations, Func<Individual, bool> done)
        {
            int generation = 0;
            while (generation < generations && !(algorithm.Best is Individual best && done(best)))
            {
                algorithm.NextGeneration();
                generation++;
                SideGenerationsRun++;
            }
            return generation;
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
                Network composite = ModuleEdits.Insert(host, chosen, library.NextInstance(), maxNeurons, library, random);
                if (composite != host)
                {
                    composites.Add(composite);
                }
            }
            return composites;
        }
    }
}
