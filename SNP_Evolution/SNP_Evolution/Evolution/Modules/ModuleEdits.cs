using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
{
    // The library and how modules are treated, for building the mutation. Tracker is set for the main run only, so
    // credit and harvesting follow the task the run is scored on.
    public sealed record ModuleSupport(ModuleLibrary Library, bool Freeze = true, ModuleTracker? Tracker = null);

    public static class ModuleEdits
    {
        // A module cut from around an output takes the output half the time, so a part that makes the right output can take over.
        public static Network Insert(Network network, Module module, int instance, int maxNeurons, ModuleLibrary library, Random random)
        {
            int offset = network.Neurons.Count;
            if (offset == 0 || offset + module.Body.Neurons.Count > maxNeurons)
            {
                return network;
            }
            var tag = new ModuleTag(module.Id, instance);
            IReadOnlyList<PartCopy> copies = module.Part != null ? PartWiring.Copies(network, library) : Array.Empty<PartCopy>();
            HashSet<int> typed = copies.SelectMany(copy => copy.Positions).ToHashSet();
            bool takesOutput = module.Body.Neurons.Any(neuron => neuron.IsOutput) && random.Next(2) == 0;
            int moduleOutput = offset + module.Body.Neurons.ToList().FindIndex(neuron => neuron.IsOutput) + 1;
            // An old output inside a part copy keeps its synapses, since one to the new output would join two ports untyped.
            List<Neuron> neurons = network.Neurons
                .Select((neuron, index) => takesOutput && neuron.IsOutput
                    ? neuron.WithRoles(false, neuron.IsInput).WithConnections(typed.Contains(index + 1) ? neuron.Connections : neuron.Connections.Append(moduleOutput))
                    : neuron)
                .ToList();
            neurons.AddRange(module.Body.Neurons.Select(neuron => neuron
                .WithConnections(neuron.Connections.Select(target => target + offset))
                .WithRoles(takesOutput && neuron.IsOutput, false)
                .WithModule(tag)));
            if (module.Part is LibraryPart part)
            {
                PartWiring.WirePorts(neurons, copies, new PartCopy(tag, part, Enumerable.Range(offset + 1, module.Body.Neurons.Count).ToList()), offset, random);
                return new Network(neurons);
            }
            List<int> receivers = Enumerable.Range(0, offset).Where(index => !network.Neurons[index].IsInput).ToList();
            foreach (int port in module.Cut.Inputs)
            {
                int sender = random.Next(offset);
                neurons[sender] = neurons[sender].WithConnections(neurons[sender].Connections.Append(offset + port + 1));
            }
            foreach (int port in module.Cut.Outputs.Where(port => !(takesOutput && module.Body.Neurons[port].IsOutput)))
            {
                int own = offset + port;
                neurons[own] = neurons[own].WithConnections(neurons[own].Connections.Append(receivers[random.Next(receivers.Count)] + 1));
            }
            return new Network(neurons);
        }

        // Copies of modules in the network, each as the positions of its neurons in order.
        public static Dictionary<ModuleTag, List<int>> Instances(Network network)
        {
            var instances = new Dictionary<ModuleTag, List<int>>();
            for (int index = 0; index < network.Neurons.Count; index++)
            {
                if (network.Neurons[index].Module is ModuleTag tag)
                {
                    if (!instances.TryGetValue(tag, out List<int>? positions))
                    {
                        instances[tag] = positions = new List<int>();
                    }
                    positions.Add(index + 1);
                }
            }
            return instances;
        }

        // Whether every module copy in before is still in after with the same rules and synapses among its neurons.
        // Initial spikes may change, so a frozen counter can still be set to count further.
        public static bool KeepsModules(Network before, Network after)
        {
            Dictionary<ModuleTag, List<int>> previous = Instances(before);
            if (previous.Count == 0)
            {
                return true;
            }
            Dictionary<ModuleTag, List<int>> current = Instances(after);
            return previous.All(instance => current.TryGetValue(instance.Key, out List<int>? positions)
                && Inside(before, instance.Value) == Inside(after, positions));
        }

        internal static string Inside(Network network, IReadOnlyList<int> positions)
        {
            var rank = positions.Select((position, order) => (position, order)).ToDictionary(pair => pair.position, pair => pair.order);
            return string.Join(";", positions.Select(position =>
            {
                Neuron neuron = network.Neurons[position - 1];
                return ModuleCuts.RulesText(neuron) + ">" + string.Join(",", neuron.Connections.Where(rank.ContainsKey).Select(target => rank[target]));
            }));
        }
    }

    // Puts a copy of a module from the library into the network.
    public sealed class InsertModule : IMutation
    {
        private readonly ModuleLibrary library;
        private readonly GenomeSpace space;

        public InsertModule(ModuleLibrary library, GenomeSpace space)
        {
            this.library = library;
            this.space = space;
        }

        public Network Mutate(Network network, Random random) =>
            library.Choose(random) is Module module ? ModuleEdits.Insert(network, module, library.NextInstance(), space.MaxNeurons, library, random) : network;
    }

    // Frees one module copy, so its neurons can change like any other from now on.
    public sealed class DissolveModule : IMutation
    {
        public Network Mutate(Network network, Random random)
        {
            List<KeyValuePair<ModuleTag, List<int>>> instances = ModuleEdits.Instances(network).ToList();
            if (instances.Count == 0)
            {
                return network;
            }
            HashSet<int> freed = instances[random.Next(instances.Count)].Value.ToHashSet();
            return new Network(network.Neurons.Select((neuron, index) => freed.Contains(index + 1) ? neuron.WithModule(null) : neuron).ToList());
        }
    }

    // Lets another edit change anything but the inside of a module copy: an edit that would is tried again, a few
    // times, and otherwise the network is left as it was.
    public sealed class ProtectModules : IMutation
    {
        private const int Attempts = 3;

        private readonly IMutation edit;

        public ProtectModules(IMutation edit) => this.edit = edit;

        public Network Mutate(Network network, Random random)
        {
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                Network changed = edit.Mutate(network, random);
                if (ModuleEdits.KeepsModules(network, changed))
                {
                    return changed;
                }
            }
            return network;
        }
    }
}
