using SnpEvolution.Model;

namespace SnpEvolution.Search.Fitness
{
    // A 64-bit hash of everything that decides how a network behaves: each neuron's spikes, roles, rules and synapses,
    // in neuron order. Module tags are left out since they only label. Networks that differ only in how their neurons
    // are numbered hash differently, so counting repeats by it gives a lower bound.
    public static class NetworkFingerprint
    {
        private const ulong Offset = 14695981039346656037;
        private const ulong Prime = 1099511628211;

        public static ulong Of(Network network)
        {
            ulong hash = Offset;
            void Add(long value) => hash = (hash ^ (ulong)value) * Prime;
            Add(network.Neurons.Count);
            foreach (Neuron neuron in network.Neurons)
            {
                Add(neuron.InitialSpikes);
                Add((neuron.IsInput ? 1 : 0) | (neuron.IsOutput ? 2 : 0));
                Add(neuron.Rules.Count);
                foreach (Rule rule in neuron.Rules)
                {
                    Add(rule.Expression.Length);
                    foreach (char symbol in rule.Expression)
                    {
                        Add(symbol);
                    }
                    Add(rule.Delay);
                    Add((rule.Fire ? 1 : 0) | (rule.Axonal ? 2 : 0) | (rule.Consume.HasValue ? 4 : 0));
                    Add(rule.Consume ?? 0);
                    Add(rule.Produce);
                }
                Add(neuron.Connections.Count);
                foreach (int target in neuron.Connections)
                {
                    Add(target);
                }
            }
            return hash;
        }
    }
}
