using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Export
{
    // One neuron on one step of a deterministic run: the spikes it held when the step began, whether it applied a rule
    // (NIR's spike), what it sent along every synapse, and what it held once the step's spikes were delivered.
    public readonly record struct NeuronStep(long Held, bool Applied, long Sent, long After);

    // A deterministic network's run, step by step, as exporters are checked against it. Steps[t][i] is neuron i on step t.
    public sealed record SpikeTrace(IReadOnlyList<IReadOnlyList<NeuronStep>> Steps)
    {
        // Every pair of rules in one neuron that could both apply to some count of spikes, which hardware would have to
        // choose between; empty when the network is deterministic.
        public static IReadOnlyList<string> Choices(Network network)
        {
            var choices = new List<string>();
            for (int index = 0; index < network.Neurons.Count; index++)
            {
                IReadOnlyList<Rule> rules = network.Neurons[index].Rules;
                for (int first = 0; first < rules.Count; first++)
                {
                    for (int second = first + 1; second < rules.Count; second++)
                    {
                        if (SharedCount(rules[first], rules[second]) is long count)
                        {
                            choices.Add($"Neuron {index + 1} could apply rule {first + 1} ({NetworkNotation.Rule(rules[first])}) or rule {second + 1} " +
                                $"({NetworkNotation.Rule(rules[second])}) when it holds {count} spike(s).");
                        }
                    }
                }
            }
            return choices;
        }

        // Runs the network on the input for the given number of steps. Throws when it is not deterministic.
        public static SpikeTrace Run(Network network, InputSpikes input, int steps)
        {
            if (Choices(network).FirstOrDefault() is string choice)
            {
                throw new ArgumentException($"The network is not deterministic: {choice}");
            }
            var simulation = new NetworkSimulation(CompiledNetwork.Of(network), null, input, OutputTiming.Interval);
            var rows = new List<IReadOnlyList<NeuronStep>>();
            var chosen = new int[network.Neurons.Count];
            Span<int> buffer = new int[Math.Max(1, simulation.MaxRulesPerNeuron)];
            for (int step = 0; step < steps; step++)
            {
                IReadOnlyList<long> held = simulation.Spikes;
                for (int neuron = 0; neuron < chosen.Length; neuron++)
                {
                    chosen[neuron] = simulation.CollectApplicableRules(neuron, buffer) == 0 ? NetworkSimulation.NoRule : buffer[0];
                }
                simulation.Apply(chosen);
                IReadOnlyList<long> sent = simulation.Sent;
                IReadOnlyList<long> after = simulation.Spikes;
                rows.Add(Enumerable.Range(0, chosen.Length).Select(neuron => new NeuronStep(held[neuron], chosen[neuron] != NetworkSimulation.NoRule, sent[neuron], after[neuron])).ToList());
            }
            return new SpikeTrace(rows);
        }

        // The steps a contract task watches each case for, and each case's input; ContractTask encodes them the same way.
        public static IReadOnlyList<(string Label, InputSpikes Input, int Steps)> Cases(Contract contract)
        {
            var task = new Evolution.Tasks.ContractTask(contract);
            return contract.Cases.Select((@case, index) => ($"case {index + 1} {@case.Label(contract.DataIn)}".TrimEnd(), task.Cases[index].Input, task.StepsNeeded)).ToList();
        }

        // "step neuron sent after" per line, neurons numbered from 1, as the Verilog testbench prints them.
        public string SentAndHeld()
        {
            var text = new StringBuilder();
            for (int step = 0; step < Steps.Count; step++)
            {
                for (int neuron = 0; neuron < Steps[step].Count; neuron++)
                {
                    NeuronStep row = Steps[step][neuron];
                    text.Append(string.Create(CultureInfo.InvariantCulture, $"{step} {neuron + 1} {row.Sent} {row.After}\n"));
                }
            }
            return text.ToString();
        }

        // The most spikes any neuron held on any step, which sizes a hardware counter.
        public long MostHeld => Steps.SelectMany(step => step).Select(row => Math.Max(row.Held, row.After)).DefaultIfEmpty(0).Max();

        // Conditions are eventually periodic, so if two rules share a count they share one below both tails, the larger
        // consumption and one full cycle of both periods.
        private static long? SharedCount(Rule first, Rule second)
        {
            long period = Lcm(first.Condition.Period, second.Condition.Period);
            long limit = Math.Max(first.Condition.TailLength, second.Condition.TailLength) + Math.Max(first.Consume ?? 0, second.Consume ?? 0) + period;
            for (long count = 0; count <= limit; count++)
            {
                if (first.Applies(count) && second.Applies(count))
                {
                    return count;
                }
            }
            return null;
        }

        private static long Lcm(long first, long second)
        {
            long a = first, b = second;
            while (b != 0)
            {
                (a, b) = (b, a % b);
            }
            return first / a * second;
        }
    }
}
