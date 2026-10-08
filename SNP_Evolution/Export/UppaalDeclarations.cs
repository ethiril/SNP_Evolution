using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SnpEvolution.Model;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Export
{
    // The global declarations of a part's Uppaal model: its constants, its state and the functions the automata call.
    internal static class UppaalDeclarations
    {
        public static string Of(Part part, ContractTask task, bool deterministic)
        {
            var text = new StringBuilder();
            text.Append(Constants(part, task));
            text.Append(State(part.Contract));
            if (deterministic)
            {
                text.Append(ExpectedTrace(part.Network, task));
            }
            text.AppendLine();
            text.Append(RuleFunctions(part.Network));
            text.Append(ReleaseFunction());
            text.Append(JudgeFunction(part.Contract));
            text.Append(DeliverFunction(part, task, deterministic));
            return text.ToString();
        }

        // Only a contract whose cases differ in their earliest done needs a per-case table, so other models stay unchanged.
        private static bool EarliestVaries(Contract contract) => contract.Cases.Any(@case => contract.EarliestDone(@case) != contract.MinLatency);

        public static string Array(IEnumerable<int> values) => "{" + string.Join(", ", values.Select(value => value.ToString(CultureInfo.InvariantCulture))) + "}";

        private static string Constants(Part part, ContractTask task)
        {
            Contract contract = part.Contract;
            Network network = part.Network;
            List<Port> dataOut = contract.DataOut.ToList();
            List<Port> done = contract.Done.ToList();
            int slots = network.Neurons.SelectMany(neuron => neuron.Rules).Where(rule => rule.DelayKind == DelayKind.Axonal).Select(rule => rule.Delay).DefaultIfEmpty(0).Max() + 1;
            var text = new StringBuilder();
            text.AppendLine($"// {contract.Name}: {network.Neurons.Count} neurons, {task.Cases.Count} case(s), runs of at most {task.StepsNeeded} steps.");
            text.AppendLine($"const int NEURONS = {network.Neurons.Count};");
            text.AppendLine($"const int CASES = {task.Cases.Count};");
            text.AppendLine($"const int HORIZON = {task.StepsNeeded};");
            text.AppendLine($"const int AFTER_DONE = {task.StepsAfterDone};");
            text.AppendLine($"const int SLOTS = {slots};");
            text.AppendLine($"const int MIN_LATENCY = {contract.MinLatency};");
            if (EarliestVaries(contract))
            {
                text.AppendLine($"const int EARLIEST[CASES] = {Array(contract.Cases.Select(contract.EarliestDone))};");
            }
            text.AppendLine($"const int MAX_LATENCY = {contract.MaxLatency};");
            text.AppendLine($"const int INITIAL[NEURONS] = {Array(network.Neurons.Select(neuron => (int)neuron.InitialSpikes))};");
            text.AppendLine($"const int START[CASES] = {Array(task.StartSteps)};");
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
            return text.ToString();
        }

        // The verdicts are plain flags, so no query calls a function.
        private static string State(Contract contract)
        {
            int outs = contract.DataOut.Count();
            var text = new StringBuilder();
            text.AppendLine("// What the contract's ports did, as the engine's PortRecorder records it.");
            text.AppendLine("bool quietBroken = false;");
            text.AppendLine("int doneFirings = 0;");
            text.AppendLine("int doneSlot = -1;");
            text.AppendLine("int firstDone = -1;");
            if (outs > 0)
            {
                text.AppendLine("int outSum[OUTS];");
                text.AppendLine("int outFirings[OUTS];");
                text.AppendLine("int outFirst[OUTS] = " + Array(Enumerable.Repeat(-1, outs)) + ";");
                text.AppendLine("int outSecond[OUTS] = " + Array(Enumerable.Repeat(-1, outs)) + ";");
                text.AppendLine("int outLast[OUTS] = " + Array(Enumerable.Repeat(-1, outs)) + ";");
            }
            text.AppendLine("bool ended = false;");
            text.AppendLine("bool doneOk = false;");
            text.AppendLine("bool backOk = false;");
            text.AppendLine("bool onTimeOk = false;");
            text.AppendLine("bool traceBroken = false;");
            return text.ToString();
        }

        private static string ExpectedTrace(Network network, ContractTask task)
        {
            IEnumerable<string> rows = task.Cases.Select(@case =>
            {
                SpikeTrace trace = SpikeTrace.Run(network, @case.Input, task.StepsNeeded);
                return "{" + string.Join(", ", trace.Steps.Select(row => Array(row.Select(neuron => (int)neuron.After)))) + "}";
            });
            var text = new StringBuilder();
            text.AppendLine();
            text.AppendLine("// What every neuron holds after each step of our engine's run of each case.");
            text.AppendLine($"const int EXPECT[CASES][HORIZON][NEURONS] = {{{string.Join(",\n  ", rows)}}};");
            return text.ToString();
        }

        // applies_i_r(k) reads rule r of neuron i's lasso table, once the neuron holds enough to consume.
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
                    text.AppendLine($"  if (k < {rules[index].LeastHeld}) return false;");
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

        private static string JudgeFunction(Contract contract)
        {
            List<Port> dataOut = contract.DataOut.ToList();
            var text = new StringBuilder();
            text.AppendLine("// The verdicts, once the run is over, as ContractTask scores a run.");
            text.AppendLine("void judge() {");
            text.AppendLine("  int i;");
            text.AppendLine("  backOk = true;");
            text.AppendLine("  for (i = 0; i < NEURONS; i++) { if (spikes[i] != INITIAL[i]) backOk = false; }");
            string earliest = EarliestVaries(contract) ? "EARLIEST[caseNo]" : "MIN_LATENCY";
            text.AppendLine($"  onTimeOk = firstDone >= 0 && firstDone - (START[caseNo] + 1) >= {earliest} && firstDone - (START[caseNo] + 1) <= MAX_LATENCY;");
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
            if (contract.TogetherTriggers)
            {
                text.AppendLine("  // Triggers that fire do so on one step.");
                foreach (int first in triggers)
                {
                    foreach (int second in triggers.Where(slot => slot > first))
                    {
                        text.AppendLine($"  if (outFirst[{first}] >= 0 && outFirst[{second}] >= 0 && outFirst[{first}] != outFirst[{second}]) doneOk = false;");
                    }
                }
            }
            text.AppendLine("}");
            text.AppendLine();
            return text.ToString();
        }

        // deliver() sends what was released, adds the environment's input and records what the ports did, then ends the run where the exhaustive engine does.
        private static string DeliverFunction(Part part, ContractTask task, bool deterministic)
        {
            Network network = part.Network;
            Contract contract = part.Contract;
            List<Port> dataOut = contract.DataOut.ToList();
            List<Port> done = contract.Done.ToList();
            List<int> inputNeurons = network.Neurons.Select((neuron, index) => (neuron, index)).Where(pair => pair.neuron.IsInput).Select(pair => pair.index).ToList();
            var text = new StringBuilder();
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
            List<(Port Port, int Neuron)> watched = PortLayout.OutPorts(contract).Select(port => (port, part.Binding[port.Name] - 1)).ToList();
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
    }
}
