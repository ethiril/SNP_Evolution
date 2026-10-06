using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Genome;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Search
{
    // Evolves a network that already solves the task into smaller ones that still do, in the spirit of superoptimisers
    // such as STOKE (Schkufza, Sharma and Aiken 2013) and of evolving smaller circuits from a working one (Vasicek and
    // Sekanina): every network starts as the working one, children compete on fitness first and size second, so a
    // smaller child survives only if it is as fit, and a smaller network only counts once it passes the retests that
    // stop a run. Shrinking uses the usual edits apart from those that only add, plus bypassing and merging neurons.
    public sealed class ShrinkRun
    {
        private readonly FitnessEvaluator evaluator;
        private readonly MuPlusLambdaStrategy algorithm;
        private readonly Action<string> log;

        public ShrinkRun(Network start, FitnessEvaluator evaluator, NetworkFactory factory, int population, Random random, Action<string> log)
        {
            this.evaluator = evaluator;
            this.log = log;
            Start = start;
            Smallest = start;
            algorithm = new MuPlusLambdaStrategy(Math.Max(1, population / 5), population, random, () => start, evaluator, Edits(factory));
        }

        public Network Start { get; }

        // The smallest network found that reliably solves the task, which is the start until a smaller one does.
        public Network Smallest { get; private set; }

        public int Generations { get; private set; }

        public IReadOnlyList<IReadOnlyList<float>> FitnessHistory => algorithm.FitnessHistory;

        // The edits a shrink run makes: none that only make a network bigger.
        public static WeightedMutation Edits(NetworkFactory factory)
        {
            var growing = new HashSet<string> { "Add neuron", "Add rule", "Split synapse", "Duplicate neuron" };
            List<WeightedEdit> edits = WeightedMutation.Structural(1, factory).Edits.Where(edit => !growing.Contains(edit.Name)).ToList();
            edits.Add(new WeightedEdit("Bypass neuron", new BypassNeuron(), 1));
            edits.Add(new WeightedEdit("Merge neurons", new MergeNeurons(), 1));
            return new WeightedMutation(1, edits);
        }

        // False, after saying why, when the start does not reliably solve the task, since then there is nothing to keep.
        public bool Run(int generations)
        {
            if (!evaluator.IsReliablySolved(Start))
            {
                log($"The starting network does not reliably solve {evaluator.Task.Name} (fitness {evaluator.Evaluate(Start).Fitness:0.000}), so there is nothing to shrink.");
                return false;
            }
            log($"Shrinking from {Describe(Start)}.");
            for (Generations = 0; Generations < generations; Generations++)
            {
                algorithm.NextGeneration();
                foreach (Individual candidate in algorithm.Population
                    .Where(individual => Solved.Solves(individual.Fitness) && individual.Genes.Size < Smallest.Size)
                    .OrderBy(individual => individual.Genes.Size)
                    .Take(3))
                {
                    if (evaluator.IsReliablySolved(candidate.Genes))
                    {
                        Smallest = candidate.Genes;
                        log($"Shrink generation {Generations}: {Describe(Smallest)}.");
                        break;
                    }
                }
            }
            return true;
        }

        public static string Describe(Network network) =>
            $"{network.Neurons.Count} neurons, {network.RuleCount} rules, {network.SynapseCount} synapses (size {network.Size})";
    }
}
