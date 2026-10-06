using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Compilation;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Contracts
{
    // How a part is evolved for one contract. Budget is search evaluations; ShrinkBudget is spent after a part is found
    // making it smaller. ExtraNeurons is how far past the port neurons a network may grow.
    public sealed record PartSearchSettings(
        long Budget,
        long ShrinkBudget,
        int Population,
        AlgorithmChoice Algorithm,
        Func<ISimulationEngine> CreateEngine,
        bool Lexicase = true,
        int ExtraNeurons = 4,
        int MaxDelay = 3,
        int MaxInitialSpikes = 4,
        int MaxProduce = 2);

    // A network run on every case of a contract on the exhaustive engine. Latency is the slowest case's, from the step
    // start reaches the part to the step done fires; Behaviour is what ContractTask.Behaviour reads. MeetsContract says
    // every check of every case passed on every computation, which needs every case to have been followed exactly;
    // Description names the checks that failed.
    public sealed record PartMeasurement(Network Network, HardwareCost Cost, int Latency, string Behaviour, bool MeetsContract, string Description);

    // Evaluations counts every network scored for the contract: search, shrinking and verification.
    public sealed record PartOutcome(Contract Contract, int Seed, long Evaluations, Part? Part, PartMeasurement? Measurement)
    {
        public bool Solved => Part != null;
    }

    // Evolves a part from scratch for a contract, verifies it on the exhaustive engine, then shrinks it with MAP-Elites
    // over hardware cost cells, keeping the cheapest network that still verifies.
    public static class PartEvolution
    {
        public const int VerifyConfigurations = 50_000;

        private const int Repetitions = 20;
        private const float MutationRate = 0.5f;

        public static PartOutcome Evolve(Contract contract, int runSeed, PartSearchSettings settings, Action<string> log)
        {
            int seed = SeedFor(runSeed, contract.Name);
            var random = new Random(seed);
            var task = new ContractTask(contract);
            var space = new GenomeSpace(
                InputCount: task.InputCount,
                RuleForm: RuleForm.Standard,
                MinNeurons: task.Binding.NeuronsNeeded,
                MaxNeurons: task.Binding.NeuronsNeeded + settings.ExtraNeurons,
                MaxDelay: settings.MaxDelay,
                MaxInitialSpikes: settings.MaxInitialSpikes,
                MaxProduce: settings.MaxProduce);
            var factory = new NetworkFactory(space, new ExpressionGenerator(ExpressionGenerator.ExperimentalTemplates, 4, random), random);
            var options = new SimulationOptions(task.StepsNeeded, Repetitions, OutputTiming.Interval);
            var search = new FitnessEvaluator(settings.CreateEngine(), task, options, solvedRetestCount: 3, random);
            var verifier = new Verifier(task, options);
            IGeneticAlgorithm algorithm = settings.Algorithm.Create(new EvolutionContext(
                settings.Population, MutationRate, random, factory.NewNetwork, search, factory, _ => { }, Lexicase: settings.Lexicase));

            PartMeasurement? found = null;
            while (found == null && search.Evaluations < settings.Budget)
            {
                algorithm.NextGeneration();
                found = algorithm.Population.Where(individual => individual.Fitness == 1).Take(3).Select(individual => verifier.Verify(individual.Genes))
                    .FirstOrDefault(verified => verified != null);
            }
            if (found == null)
            {
                string failing = string.Join("; ", (algorithm.Best?.Description ?? "").Split(Environment.NewLine).Take(3));
                log($"{contract.Name}: not solved in {search.Evaluations} evaluations (best fitness {algorithm.Best?.Fitness ?? 0:0.000}, failing {failing}).");
                return new PartOutcome(contract, seed, search.Evaluations + verifier.Evaluations, null, null);
            }
            log($"{contract.Name}: solved after {search.Evaluations} evaluations, {found.Cost}.");
            var shrink = new FitnessEvaluator(settings.CreateEngine(), task, options, solvedRetestCount: 3, random);
            found = Shrink(found, shrink, verifier, factory, settings, random);
            log($"{contract.Name}: kept {found.Cost}, latency {found.Latency}.");
            long evaluations = search.Evaluations + shrink.Evaluations + verifier.Evaluations;
            return new PartOutcome(contract, seed, evaluations, new Part(contract, found.Network, task.Binding), found);
        }

        // A seed of the contract's own, so running a subset of contracts gives each the part a full run would.
        public static int SeedFor(int runSeed, string contractName)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char letter in contractName)
                {
                    hash = (hash ^ letter) * 16777619;
                }
                return (int)(hash ^ (uint)runSeed * 2654435761) & int.MaxValue;
            }
        }

        public static PartMeasurement Measure(Part part) => Measure(part.Network, part.Task());

        public static PartMeasurement Measure(Network network, ContractTask task) =>
            new Verifier(task, new SimulationOptions(task.StepsNeeded, Repetitions, OutputTiming.Interval)).Measure(network);

        // Every cell of the archive starts from the part; each generation the cheapest solving elites are verified, and
        // the cheapest that verifies is kept.
        private static PartMeasurement Shrink(PartMeasurement start, FitnessEvaluator evaluator, Verifier verifier, NetworkFactory factory, PartSearchSettings settings, Random random)
        {
            var archive = new MapElites(settings.Population, random, () => start.Network, evaluator, new Operators.NeuronCrossover(), ShrinkRun.Edits(factory),
                cells: HardwareCost.Cell);
            PartMeasurement smallest = start;
            var tried = new HashSet<string>();
            while (evaluator.Evaluations < settings.ShrinkBudget)
            {
                archive.NextGeneration();
                foreach (Individual candidate in archive.Population
                    .Where(individual => individual.Fitness == 1 && HardwareCost.Of(individual.Genes).CompareTo(smallest.Cost) < 0)
                    .OrderBy(individual => HardwareCost.Of(individual.Genes), HardwareCost.SmallestFirst)
                    .Take(3))
                {
                    if (tried.Add(NetworkNotation.Format(candidate.Genes)) && verifier.Verify(candidate.Genes) is PartMeasurement smaller && smaller.Cost.CompareTo(smallest.Cost) < 0)
                    {
                        smallest = smaller;
                        break;
                    }
                }
            }
            return smallest;
        }

        private sealed class Verifier
        {
            private readonly ContractTask task;
            private readonly ExhaustiveCpuEngine engine = new ExhaustiveCpuEngine(VerifyConfigurations);
            private readonly SimulationOptions options;

            public Verifier(ContractTask task, SimulationOptions options)
            {
                this.task = task;
                this.options = options;
            }

            public long Evaluations { get; private set; }

            // Null unless the network meets the contract.
            public PartMeasurement? Verify(Network network) => Measure(network) is { MeetsContract: true } measurement ? measurement : null;

            public PartMeasurement Measure(Network network)
            {
                Evaluations++;
                var trials = task.Cases.Select(@case => new Trial(network, @case.Input, @case.Readout, @case.Watch)).ToList();
                IReadOnlyList<TrialResult> results = engine.Run(trials, options, new Random(0));
                bool exact = results.All(result => result.Exact);
                bool meets = exact && task.Checks(results).All(check => check == 1);
                string description = exact ? task.Describe(results) : "some cases have too many computations to follow exactly";
                return new PartMeasurement(network, HardwareCost.Of(network, results), task.Niche(results)?.Item2 ?? 0, task.Behaviour(results), meets, description);
            }
        }
    }
}
