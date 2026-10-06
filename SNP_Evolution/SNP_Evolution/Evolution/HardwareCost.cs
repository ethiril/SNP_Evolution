using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution
{
    // What a network costs to build in hardware, beyond its neuron count: synapses are wires, distinct rules are the
    // comparators a neuron needs, register width is the most spikes any neuron holds (the counter's bits), and the lasso
    // table is the lookup memory its rule conditions take (SpikeCondition.TailLength + Period, summed over rules).
    // Costs order by neurons, then synapses, then rules, then register width; distinct rules and the lasso table only
    // break ties after those, so every comparison gives the same order.
    public sealed record HardwareCost(int Neurons, int Synapses, int DistinctRules, int Rules, long RegisterWidth, int LassoTable)
        : IComparable<HardwareCost>
    {
        public static readonly IComparer<HardwareCost> SmallestFirst = Comparer<HardwareCost>.Create((first, second) => first.CompareTo(second));

        // The static measures, with the register width the network starts with; a run can only make it wider.
        public static HardwareCost Of(Network network) => new HardwareCost(
            network.Neurons.Count,
            network.SynapseCount,
            network.Neurons.SelectMany(neuron => neuron.Rules).Select(RuleKey).Distinct().Count(),
            network.RuleCount,
            network.Neurons.Select(neuron => neuron.InitialSpikes).DefaultIfEmpty(0).Max(),
            network.Neurons.SelectMany(neuron => neuron.Rules).Sum(rule => rule.Condition.TailLength + rule.Condition.Period));

        // With the register width the runs reached, from the Ports readouts of a contract's cases.
        public static HardwareCost Of(Network network, IReadOnlyList<TrialResult> results)
        {
            HardwareCost cost = Of(network);
            long widest = results.SelectMany(result => result.PortRuns).Select(run => run.MostHeld).DefaultIfEmpty(0).Max();
            return cost with { RegisterWidth = Math.Max(cost.RegisterWidth, widest) };
        }

        // A MAP-Elites cell, so a shrink run keeps the best network at every neuron and synapse count.
        public static (int, int) Cell(Network network) => (network.Neurons.Count, network.SynapseCount);

        public int CompareTo(HardwareCost? other)
        {
            if (other is null)
            {
                return 1;
            }
            int order = Neurons.CompareTo(other.Neurons);
            order = order != 0 ? order : Synapses.CompareTo(other.Synapses);
            order = order != 0 ? order : Rules.CompareTo(other.Rules);
            order = order != 0 ? order : RegisterWidth.CompareTo(other.RegisterWidth);
            order = order != 0 ? order : DistinctRules.CompareTo(other.DistinctRules);
            return order != 0 ? order : LassoTable.CompareTo(other.LassoTable);
        }

        public override string ToString() =>
            $"{Neurons} neurons, {Synapses} synapses, {Rules} rules ({DistinctRules} distinct), register width {RegisterWidth}, lasso table {LassoTable}";

        private static string RuleKey(Rule rule) => $"{rule.Expression}:{rule.Delay}:{rule.Fire}:{rule.Consume}:{rule.Produce}:{rule.Axonal}";
    }
}
