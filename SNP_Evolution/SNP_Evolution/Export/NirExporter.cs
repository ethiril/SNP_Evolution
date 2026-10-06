using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Export
{
    // A hardware-profile network as tools/snp_nir.py turns it into a NIR graph, with the cases to co-simulate it on.
    // NIR files are HDF5, which .NET has no writer for, so this is the intermediate the Python nir package reads.
    //
    // The mapping is exact only within the profile: each neuron is an integrate-and-fire neuron with integer threshold k,
    // reset to zero and unit weights, and its axonal delay a NIR delay. Time is discrete with one SN P step per time
    // step (dt = 1): a spike sent on step t is integrated on step t + 1, the one-step latency a discrete NIR backend puts
    // on a recurrent connection, and the environment's spike on step t is presented at time step t + 1. tools/snp_nir.py
    // and RESEARCH.md ("Exporting to NIR") write the mapping out in full.
    public sealed record NirDescription(
        [property: JsonProperty("format")] string Format,
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("neurons")] int Neurons,
        [property: JsonProperty("inputs")] IReadOnlyList<NirPort> Inputs,
        [property: JsonProperty("outputs")] IReadOnlyList<NirPort> Outputs,
        [property: JsonProperty("thresholds")] IReadOnlyList<int?> Thresholds,
        [property: JsonProperty("fires")] IReadOnlyList<bool> Fires,
        [property: JsonProperty("delays")] IReadOnlyList<int> Delays,
        [property: JsonProperty("synapses")] IReadOnlyList<int[]> Synapses,
        [property: JsonProperty("cases")] IReadOnlyList<NirCase> Cases);

    public sealed record NirPort([property: JsonProperty("name")] string Name, [property: JsonProperty("neuron")] int Neuron);

    // Per step, the neurons (from 1) that applied their rule, which is when the IF neuron fires, and what every neuron
    // held after applying it and before the step's spikes arrived, which is the IF membrane potential after reset.
    public sealed record NirCase(
        [property: JsonProperty("label")] string Label,
        [property: JsonProperty("input")] IReadOnlyList<IReadOnlyList<int>> Input,
        [property: JsonProperty("applied")] IReadOnlyList<IReadOnlyList<int>> Applied,
        [property: JsonProperty("held_after_rule")] IReadOnlyList<IReadOnlyList<long>> HeldAfterRule);

    public static class NirExporter
    {
        public const string Format = "snp-nir/1";

        // The part's ports and every contract case.
        public static NirDescription Export(Part part) =>
            Export(part.Network, part.Contract.Name, part.Ports().Select(port => (port.Port.Name, port.Position, port.Port.Direction == PortDirection.In)).ToList(),
                SpikeTrace.Cases(part.Contract));

        // Throws ArgumentException naming the rule that breaks the profile, since outside it the mapping is not exact.
        public static NirDescription Export(Network network, string name, IReadOnlyList<(string Name, int Neuron, bool IsInput)> ports,
            IReadOnlyList<(string Label, InputSpikes Input, int Steps)> cases)
        {
            if (HardwareProfile.Problems(network).FirstOrDefault() is string problem)
            {
                throw new ArgumentException($"Only networks within the hardware profile export to NIR, where they map exactly onto integrate-and-fire neurons. {problem}");
            }
            Rule? RuleOf(Neuron neuron) => neuron.Rules.Count == 0 ? null : neuron.Rules[0];
            return new NirDescription(
                Format,
                name,
                network.Neurons.Count,
                ports.Where(port => port.IsInput).Select(port => new NirPort(port.Name, port.Neuron)).ToList(),
                ports.Where(port => !port.IsInput).Select(port => new NirPort(port.Name, port.Neuron)).ToList(),
                network.Neurons.Select(neuron => RuleOf(neuron) is Rule rule ? HardwareProfile.Threshold(rule.Condition) : null).ToList(),
                network.Neurons.Select(neuron => RuleOf(neuron)?.Fire ?? false).ToList(),
                network.Neurons.Select(neuron => RuleOf(neuron)?.Delay ?? 0).ToList(),
                network.Neurons.SelectMany((neuron, index) => neuron.Connections.Select(target => new[] { index + 1, target })).ToList(),
                cases.Select(@case => Case(network, @case.Label, @case.Input, @case.Steps)).ToList());
        }

        public static string ToJson(NirDescription description) => JsonConvert.SerializeObject(description, Formatting.Indented) + "\n";

        // The repository's tools/snp_nir.py, found from the working directory up.
        public static string? Script()
        {
            for (string? folder = Directory.GetCurrentDirectory(); folder != null; folder = Path.GetDirectoryName(folder))
            {
                string script = Path.Combine(folder, "tools", "snp_nir.py");
                if (File.Exists(script))
                {
                    return script;
                }
            }
            return null;
        }

        // SNP_PYTHON, else the tools/.venv the README sets up, else python3 on the path.
        public static string? Python(string script)
        {
            string venv = Path.Combine(Path.GetDirectoryName(script)!, ".venv", "bin", "python");
            return Environment.GetEnvironmentVariable("SNP_PYTHON") is string configured && configured.Length > 0 ? configured
                : File.Exists(venv) ? venv
                : ExternalTool.Find("python3");
        }

        // Null when the Python side can run, else why not.
        public static string? Unavailable()
        {
            if (Script() is not string script)
            {
                return "tools/snp_nir.py was not found above the working directory.";
            }
            if (Python(script) is not string python)
            {
                return "Python 3 is not installed.";
            }
            try
            {
                ExternalTool.Run(python, "-c \"import nir, nirtorch, norse\"", Path.GetDirectoryName(script)!);
                return null;
            }
            catch (InvalidOperationException)
            {
                return $"The nir, nirtorch and norse packages are not installed for {python}; see tools/requirements.txt.";
            }
        }

        // Writes name.nir.json and name.nir in the folder, and returns what the co-simulation printed. Throws when the
        // Python side fails or the traces differ.
        public static string WriteAndCheck(NirDescription description, string folder)
        {
            string script = Script() ?? throw new InvalidOperationException("tools/snp_nir.py was not found.");
            string python = Python(script) ?? throw new InvalidOperationException("Python 3 is not installed.");
            Directory.CreateDirectory(folder);
            string stem = Path.Combine(Path.GetFullPath(folder), FileStem(description.Name));
            File.WriteAllText(stem + ".nir.json", ToJson(description));
            ExternalTool.Run(python, $"\"{script}\" write \"{stem}.nir.json\" \"{stem}.nir\"", folder);
            return ExternalTool.Run(python, $"\"{script}\" check \"{stem}.nir.json\" \"{stem}.nir\"", folder);
        }

        public static string FileStem(string name) => new string(name.Select(letter => char.IsAsciiLetterOrDigit(letter) ? letter : '-').ToArray()).Trim('-');

        private static NirCase Case(Network network, string label, InputSpikes input, int steps)
        {
            SpikeTrace trace = SpikeTrace.Run(network, input, steps);
            return new NirCase(
                label,
                input.StepsPerInput,
                trace.Steps.Select(step => (IReadOnlyList<int>)step.Select((row, index) => (row, index)).Where(pair => pair.row.Applied).Select(pair => pair.index + 1).ToList()).ToList(),
                trace.Steps.Select(step => (IReadOnlyList<long>)step.Select(row => row.Applied ? 0 : row.Held).ToList()).ToList());
        }
    }
}
