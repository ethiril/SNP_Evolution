using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Model
{
    // Neurons that never have a choice of rule: no two of a neuron's rules apply to the same spike count. A network of
    // them has one computation per input, so the exhaustive engine follows one configuration a step rather than
    // every branch, and a contract, which must hold on every computation, has only that one to hold on.
    public static class DeterministicNeurons
    {
        public static bool Fits(Rule rule, IEnumerable<Rule> others) => others.All(other => !rule.Overlaps(other));

        public static bool Fits(Neuron neuron) => neuron.Rules.Select((rule, index) => Fits(rule, neuron.Rules.Take(index))).All(fits => fits);

        public static bool Fits(Network network) => network.Neurons.All(Fits);

        // Keeps each rule that no earlier kept rule overlaps, so the first of two rival rules wins.
        public static Neuron Conform(Neuron neuron)
        {
            var kept = new List<Rule>();
            foreach (Rule rule in neuron.Rules)
            {
                if (Fits(rule, kept))
                {
                    kept.Add(rule);
                }
            }
            return kept.Count == neuron.Rules.Count ? neuron : neuron.WithRules(kept);
        }

        // The same network when it already fits, so operators can tell nothing changed.
        public static Network Conform(Network network)
        {
            List<Neuron> neurons = network.Neurons.Select(Conform).ToList();
            return neurons.Zip(network.Neurons).All(pair => ReferenceEquals(pair.First, pair.Second)) ? network : new Network(neurons);
        }
    }
}
