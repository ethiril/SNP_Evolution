using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;

namespace SnpEvolution.Export
{
    // Model is Uppaal's XML system; Queries is its .q file, one query per line after a comment naming what it checks.
    public sealed record UppaalModel(string Name, string Model, string Queries, IReadOnlyList<string> QueryNames);

    // Writes a part as a network of Uppaal timed automata with queries for its contract. Our own translation of our
    // engine's step, since the published one (Aman and Ciobanu 2016) could not be read; see RESEARCH.md.
    //
    // A Clock automaton makes one SN P step per time unit in two phases. On release it broadcasts, and every neuron
    // automaton takes exactly one edge: one per rule that applies to the spikes it holds, chosen nondeterministically
    // when several do, a busy edge while a closing delay runs, or an idle edge. Each edge changes only that neuron's
    // variables, so the order Uppaal runs them in does not matter. The Clock then calls deliver(), which sends what was
    // released, adds the environment's input and records what the contract's ports did, as PortRecorder does. A case is
    // chosen nondeterministically before the first step, so every query covers every case. The run ends where the
    // exhaustive engine's does: StepsAfterDone after the first done, or after StepsNeeded steps. The verdicts are then
    // set as plain flags, which the queries read, so no query calls a function.
    public static class UppaalExporter
    {
        // Each query with what it checks, in the order of the .q file.
        public static readonly IReadOnlyList<(string Name, string Query)> ContractQueries = new[]
        {
            ("the run ends on every path", "A<> Clock.End"),
            ("quiet before start", "A[] !quietBroken"),
            ("done once, the right one, with the right outputs", "A[] (Clock.End imply doneOk)"),
            ("back to start", "A[] (Clock.End imply backOk)"),
            ("on time", "A[] (Clock.End imply onTimeOk)"),
        };

        public const string TraceQuery = "A[] !traceBroken";

        // Throws ArgumentException for a rule the translation does not cover (a legacy rule with a delay, which holds the
        // neuron) or a binary out-port.
        public static UppaalModel Export(Part part)
        {
            Contract contract = part.Contract;
            Network network = part.Network;
            var task = new ContractTask(contract, part.Binding);
            if (contract.DataOut.FirstOrDefault(port => port.Kind == PortKind.Binary) is Port binary)
            {
                throw new ArgumentException($"Out-port '{binary.Name}' is binary, which the Uppaal export does not read yet.");
            }
            for (int neuron = 0; neuron < network.Neurons.Count; neuron++)
            {
                if (network.Neurons[neuron].Rules.FirstOrDefault(rule => !rule.IsStandard && rule.Delay > 0 && !rule.Axonal) is Rule held)
                {
                    throw new ArgumentException($"Neuron {neuron + 1} has a legacy rule with a delay ({NetworkNotation.Rule(held)}), which holds the neuron; the Uppaal export covers closing and axonal delays only.");
                }
            }
            bool deterministic = SpikeTrace.Choices(network).Count == 0;
            List<int> starts = task.Cases.Select(@case => ContractStart(contract, @case.Input)).ToList();
            var queries = ContractQueries.ToList();
            if (deterministic)
            {
                queries.Add(("every neuron holds what our engine's run holds, on every step", TraceQuery));
            }
            string name = PartLibraryFiles.Stem(contract.Name);
            string declarations = Declarations(part, task, starts, deterministic);
            var model = new StringBuilder();
            model.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            model.Append("<!DOCTYPE nta PUBLIC '-//Uppaal Team//DTD Flat System 1.6//EN' 'http://www.it.uu.se/research/group/darts/uppaal/flat-1_6.dtd'>\n");
            model.Append("<nta>\n");
            model.Append($"<declaration>{Escape(declarations)}</declaration>\n");
            model.Append(ClockTemplate(task.Cases.Count));
            for (int neuron = 0; neuron < network.Neurons.Count; neuron++)
            {
                model.Append(NeuronTemplate(network.Neurons[neuron], neuron));
            }
            string system = "system Clock, " + string.Join(", ", Enumerable.Range(0, network.Neurons.Count).Select(neuron => $"N{neuron + 1}")) + ";";
            model.Append($"<system>{Escape(system)}</system>\n");
            model.Append("</nta>\n");
            string file = string.Concat(queries.Select(query => $"/* {query.Name} */\n{query.Query}\n"));
            return new UppaalModel(name, model.ToString(), file, queries.Select(query => query.Name).ToList());
        }

        // The step the start spike is sent on: the first input's only spike.
        private static int ContractStart(Contract contract, InputSpikes input) => input.StepsPerInput[0][0];

        private static string Declarations(Part part, ContractTask task, List<int> starts, bool deterministic)
        {
            Contract contract = part.Contract;
            Network network = part.Network;
            int neurons = network.Neurons.Count;
            int cases = task.Cases.Count;
            int horizon = task.StepsNeeded;
            List<Port> dataOut = contract.DataOut.ToList();
            List<Port> done = contract.Done.ToList();
            int maxAxonal = network.Neurons.SelectMany(neuron => neuron.Rules).Where(rule => rule.Axonal).Select(rule => rule.Delay).DefaultIfEmpty(0).Max();
            int slots = maxAxonal + 1;
            List<int> inputNeurons = network.Neurons.Select((neuron, index) => (neuron, index)).Where(pair => pair.neuron.IsInput).Select(pair => pair.index).ToList();
            int stepsAfterDone = dataOut.Where(port => port.Kind == PortKind.Binary).Select(port => port.Width).DefaultIfEmpty(0).Max() + contract.MaxLatency;

            var text = new StringBuilder();
            text.AppendLine($"// {contract.Name}: {neurons} neurons, {cases} case(s), runs of at most {horizon} steps.");
            text.AppendLine($"const int NEURONS = {neurons};");
            text.AppendLine($"const int CASES = {cases};");
            text.AppendLine($"const int HORIZON = {horizon};");
            text.AppendLine($"const int AFTER_DONE = {stepsAfterDone};");
            text.AppendLine($"const int SLOTS = {slots};");
            text.AppendLine($"const int MIN_LATENCY = {contract.MinLatency};");
            text.AppendLine($"const int MAX_LATENCY = {contract.MaxLatency};");
            text.AppendLine($"const int INITIAL[NEURONS] = {Array(network.Neurons.Select(neuron => (int)neuron.InitialSpikes))};");
            text.AppendLine($"const int START[CASES] = {Array(starts)};");
            text.AppendLine($"const int EXPECTED_DONE[CASES] = {Array(contract.Cases.Select(@case => done.FindIndex(port => port.Name == @case.Done)))};");
            if (dataOut.Count > 0)
            {
                text.AppendLine($"const int OUTS = {dataOut.Count};");
                text.AppendLine($"const int EXPECTED_OUT[CASES][OUTS] = {{{string.Join(", ", contract.Cases.Select(@case => Array(dataOut.Select(port => @case.Outputs[port.Name]))))}}};");
            }
            text.AppendLine();
            text.AppendLine("broadcast chan go;");
            text.AppendLine("int caseNo = 0;");
            text.AppendLine("int step = 0;");
            text.AppendLine($"int spikes[NEURONS] = {Array(network.Neurons.Select(neuron => (int)neuron.InitialSpikes))};");
            text.AppendLine("int emit[NEURONS];");
            text.AppendLine("int closedFor[NEURONS];");
            text.AppendLine("int pending[NEURONS];");
            text.AppendLine("bool closedNow[NEURONS];");
            text.AppendLine("int flight[NEURONS][SLOTS];");
            text.AppendLine();
            text.AppendLine("// What the contract's ports did, as the engine's PortRecorder records it.");
            text.AppendLine("bool quietBroken = false;");
            text.AppendLine("int doneFirings = 0;");
            text.AppendLine("int doneSlot = -1;");
            text.AppendLine("int firstDone = -1;");
            if (dataOut.Count > 0)
            {
                text.AppendLine("int outSum[OUTS];");
                text.AppendLine("int outFirings[OUTS];");
                text.AppendLine("int outFirst[OUTS] = " + Array(dataOut.Select(_ => -1)) + ";");
                text.AppendLine("int outSecond[OUTS] = " + Array(dataOut.Select(_ => -1)) + ";");
                text.AppendLine("int outLast[OUTS] = " + Array(dataOut.Select(_ => -1)) + ";");
            }
            text.AppendLine("bool ended = false;");
            text.AppendLine("bool doneOk = false;");
            text.AppendLine("bool backOk = false;");
            text.AppendLine("bool onTimeOk = false;");
            text.AppendLine("bool traceBroken = false;");
            if (deterministic)
            {
                text.AppendLine();
                text.AppendLine("// What every neuron holds after each step of our engine's run of each case.");
                IEnumerable<string> rows = task.Cases.Select(@case =>
                {
                    SpikeTrace trace = SpikeTrace.Run(network, @case.Input, horizon);
                    return "{" + string.Join(", ", trace.Steps.Select(row => Array(row.Select(neuron => (int)neuron.After)))) + "}";
                });
                text.AppendLine($"const int EXPECT[CASES][HORIZON][NEURONS] = {{{string.Join(",\n  ", rows)}}};");
            }
            text.AppendLine();
            text.Append(RuleFunctions(network));
            text.Append(ReleaseFunction());
            text.Append(DeliverFunction(part, inputNeurons, task, dataOut, done, deterministic));
            return text.ToString();
        }

        // matches_i_r(k) reads rule r of neuron i's lasso table; applies_i_r adds that the neuron holds enough to consume.
        private static string RuleFunctions(Network network)
        {
            var text = new StringBuilder();
            for (int neuron = 0; neuron < network.Neurons.Count; neuron++)
            {
                IReadOnlyList<Rule> rules = network.Neurons[neuron].Rules;
                for (int index = 0; index < rules.Count; index++)
                {
                    SpikeCondition condition = rules[index].Condition;
                    string table = $"TABLE_{neuron + 1}_{index + 1}";
                    text.AppendLine($"const bool {table}[{condition.Accepts.Length}] = {{{string.Join(", ", condition.Accepts.ToArray().Select(accepted => accepted ? "true" : "false"))}}};");
                    text.AppendLine($"bool applies_{neuron + 1}_{index + 1}(int k) {{");
                    text.AppendLine($"  if (k < {rules[index].Consume ?? 0}) return false;");
                    text.AppendLine($"  if (k < {condition.TailLength}) return {table}[k];");
                    text.AppendLine($"  return {table}[{condition.TailLength} + (k - {condition.TailLength}) % {condition.Period}];");
                    text.AppendLine("}");
                }
                string any = rules.Count == 0 ? "false" : string.Join(" || ", Enumerable.Range(1, rules.Count).Select(index => $"applies_{neuron + 1}_{index}(k)"));
                text.AppendLine($"bool anyApplies_{neuron + 1}(int k) {{ return {any}; }}");
            }
            text.AppendLine();
            return text.ToString();
        }

        // NetworkSimulation.ReleaseSpikes for one neuron, then the spikes its axon delivers on this step. consume is -1 for
        // consuming every spike, delay is a closing delay unless axonal.
        private static string ReleaseFunction() =>
            """
            void land(int i) {
              int arriving = flight[i][step % SLOTS];
              if (arriving > 0) { flight[i][step % SLOTS] = 0; emit[i] += arriving; }
            }
            void busy(int i) {
              emit[i] = 0;
              closedNow[i] = false;
              closedFor[i]--;
              if (closedFor[i] > 0) { closedNow[i] = true; }
              else { emit[i] = pending[i]; pending[i] = 0; }
              land(i);
            }
            void idle(int i) {
              emit[i] = 0;
              closedNow[i] = false;
              land(i);
            }
            void fireRule(int i, int consume, int produce, int delay, bool axonal) {
              emit[i] = 0;
              closedNow[i] = false;
              if (consume < 0) spikes[i] = 0; else spikes[i] -= consume;
              if (axonal) { flight[i][(step + delay) % SLOTS] += produce; }
              else if (delay > 0) { closedFor[i] = delay; pending[i] = produce; closedNow[i] = true; }
              else { emit[i] = produce; }
              land(i);
            }


            """;

        private static string DeliverFunction(Part part, List<int> inputNeurons, ContractTask task, List<Port> dataOut, List<Port> done, bool deterministic)
        {
            Network network = part.Network;
            Contract contract = part.Contract;
            var text = new StringBuilder();
            text.AppendLine("// The verdicts, once the run is over, as ContractTask scores a run.");
            text.AppendLine("void judge() {");
            text.AppendLine("  int i;");
            text.AppendLine("  backOk = true;");
            text.AppendLine("  for (i = 0; i < NEURONS; i++) { if (spikes[i] != INITIAL[i]) backOk = false; }");
            text.AppendLine("  onTimeOk = firstDone >= 0 && firstDone - (START[caseNo] + 1) >= MIN_LATENCY && firstDone - (START[caseNo] + 1) <= MAX_LATENCY;");
            text.AppendLine("  doneOk = doneFirings == 1 && doneSlot == EXPECTED_DONE[caseNo];");
            for (int slot = 0; slot < dataOut.Count; slot++)
            {
                string late = $"(outLast[{slot}] >= 0 && outLast[{slot}] >= firstDone)";
                string value = dataOut[slot].Kind switch
                {
                    PortKind.Count => $"outSum[{slot}] == EXPECTED_OUT[caseNo][{slot}]",
                    PortKind.Interval => $"outFirings[{slot}] == 2 && outSecond[{slot}] - outFirst[{slot}] == EXPECTED_OUT[caseNo][{slot}]",
                    _ => $"outFirings[{slot}] <= 1 && outFirings[{slot}] == EXPECTED_OUT[caseNo][{slot}]",
                };
                text.AppendLine($"  // {dataOut[slot].Name}, a {dataOut[slot].Kind.ToString().ToLowerInvariant()} port, read strictly between start and done.");
                text.AppendLine($"  doneOk = doneOk && !{late} && {value};");
            }
            List<int> triggers = dataOut.Select((port, slot) => (port, slot)).Where(pair => pair.port.Kind == PortKind.Trigger).Select(pair => pair.slot).ToList();
            if (contract.OrderedTriggers)
            {
                text.AppendLine("  // Triggers that fire do so on rising steps, in the order listed.");
                foreach (int first in triggers)
                {
                    foreach (int second in triggers.Where(slot => slot > first))
                    {
                        text.AppendLine($"  if (outFirst[{first}] >= 0 && outFirst[{second}] >= 0 && outFirst[{first}] >= outFirst[{second}]) doneOk = false;");
                    }
                }
            }
            text.AppendLine("}");
            text.AppendLine();
            text.AppendLine("void deliver() {");
            text.AppendLine("  int i;");
            for (int neuron = 0; neuron < network.Neurons.Count; neuron++)
            {
                IReadOnlyList<int> targets = network.Neurons[neuron].Connections;
                if (targets.Count > 0)
                {
                    text.AppendLine($"  if (emit[{neuron}] > 0) {{ {string.Concat(targets.Select(target => $"if (!closedNow[{target - 1}]) spikes[{target - 1}] += emit[{neuron}]; "))}}}");
                }
            }
            for (int input = 0; input < inputNeurons.Count && input < task.Cases[0].Input.StepsPerInput.Count; input++)
            {
                for (int @case = 0; @case < task.Cases.Count; @case++)
                {
                    foreach (IGrouping<int, int> arrivals in task.Cases[@case].Input.StepsPerInput[input].GroupBy(arrival => arrival))
                    {
                        text.AppendLine($"  if (caseNo == {@case} && step == {arrivals.Key} && !closedNow[{inputNeurons[input]}]) spikes[{inputNeurons[input]}] += {arrivals.Count()};");
                    }
                }
            }
            List<(Port Port, int Neuron)> watched = PortBinding.OutPorts(contract).Select(port => (port, part.Binding[port.Name] - 1)).ToList();
            text.AppendLine($"  if (step <= START[caseNo] && ({string.Join(" || ", watched.Select(pair => $"emit[{pair.Neuron}] > 0"))})) quietBroken = true;");
            for (int slot = 0; slot < done.Count; slot++)
            {
                int neuron = part.Binding[done[slot].Name] - 1;
                text.AppendLine($"  if (emit[{neuron}] > 0) {{ doneFirings++; if (doneSlot < 0) doneSlot = {slot}; if (firstDone < 0) firstDone = step; }}");
            }
            for (int slot = 0; slot < dataOut.Count; slot++)
            {
                int neuron = part.Binding[dataOut[slot].Name] - 1;
                text.AppendLine($"  if (emit[{neuron}] > 0 && step > START[caseNo]) {{ outSum[{slot}] += emit[{neuron}]; outFirings[{slot}]++; " +
                    $"if (outFirst[{slot}] < 0) outFirst[{slot}] = step; else if (outSecond[{slot}] < 0) outSecond[{slot}] = step; outLast[{slot}] = step; }}");
            }
            if (deterministic)
            {
                text.AppendLine("  for (i = 0; i < NEURONS; i++) { if (spikes[i] != EXPECT[caseNo][step][i]) traceBroken = true; }");
            }
            text.AppendLine("  step++;");
            text.AppendLine("  if ((firstDone >= 0 && step > firstDone + AFTER_DONE) || step == HORIZON) { ended = true; judge(); }");
            text.AppendLine("}");
            return text.ToString();
        }

        // Chooses a case, then ticks once a time unit: release, then deliver, until the run is over.
        private static string ClockTemplate(int cases) =>
            $"""
            <template>
            <name>Clock</name>
            <declaration>clock x;</declaration>
            <location id="c0"><name>Choose</name><committed/></location>
            <location id="c1"><name>Tick</name><label kind="invariant">x &lt;= 1</label></location>
            <location id="c2"><name>Released</name><committed/></location>
            <location id="c3"><name>End</name></location>
            <init ref="c0"/>
            <transition><source ref="c0"/><target ref="c1"/><label kind="select">c : int[0,{cases - 1}]</label><label kind="assignment">caseNo = c, x = 0</label></transition>
            <transition><source ref="c1"/><target ref="c2"/><label kind="guard">x == 1 &amp;&amp; !ended</label><label kind="synchronisation">go!</label><label kind="assignment">x = 0</label></transition>
            <transition><source ref="c2"/><target ref="c1"/><label kind="assignment">deliver()</label></transition>
            <transition><source ref="c1"/><target ref="c3"/><label kind="guard">ended</label></transition>
            </template>

            """;

        // One edge per rule, chosen when it applies; a busy edge while a closing delay runs; an idle edge otherwise.
        private static string NeuronTemplate(Neuron neuron, int index)
        {
            int id = index;
            int number = index + 1;
            var text = new StringBuilder();
            text.Append($"<template>\n<name>N{number}</name>\n");
            text.Append($"<location id=\"n{number}\"><name>Run</name></location>\n<init ref=\"n{number}\"/>\n");
            void Edge(string guard, string update) =>
                text.Append($"<transition><source ref=\"n{number}\"/><target ref=\"n{number}\"/><label kind=\"guard\">{Escape(guard)}</label>" +
                    $"<label kind=\"synchronisation\">go?</label><label kind=\"assignment\">{Escape(update)}</label></transition>\n");
            Edge($"closedFor[{id}] > 0", $"busy({id})");
            for (int rule = 0; rule < neuron.Rules.Count; rule++)
            {
                Rule each = neuron.Rules[rule];
                int produce = each.Fire ? (each.IsStandard ? each.Produce : 1) : 0;
                string consume = (each.Consume ?? -1).ToString(CultureInfo.InvariantCulture);
                Edge($"closedFor[{id}] == 0 && applies_{number}_{rule + 1}(spikes[{id}])",
                    $"fireRule({id}, {consume}, {produce}, {each.Delay}, {(each.Axonal ? "true" : "false")})");
            }
            Edge($"closedFor[{id}] == 0 && !anyApplies_{number}(spikes[{id}])", $"idle({id})");
            text.Append("</template>\n");
            return text.ToString();
        }

        private static string Array(IEnumerable<int> values) => "{" + string.Join(", ", values.Select(value => value.ToString(CultureInfo.InvariantCulture))) + "}";

        private static string Escape(string text) => SecurityElement.Escape(text);
    }
}
