using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Tasks
{
    // Reads one computation of a contract's case through its ports. A PortRun's firings come data out-ports first, in
    // the contract's order, then done ports. Interval, count and trigger out-ports are read strictly between start and
    // done, and a binary word follows done, bit i on step done + i.
    public sealed class ContractPorts
    {
        private readonly Contract contract;
        private readonly IReadOnlyList<int> startSteps;
        private readonly List<Port> dataOut;

        public ContractPorts(Contract contract, IReadOnlyList<int> startSteps)
        {
            this.contract = contract;
            this.startSteps = startSteps;
            dataOut = contract.DataOut.ToList();
        }

        public IReadOnlyList<Port> DataOut => dataOut;

        // The step the case sends its start spike on.
        public int Start(int caseIndex) => startSteps[caseIndex];

        // Every done port that fired, with each firing, in the contract's order of done ports.
        public List<(string Port, Firing Firing)> Dones(PortRun run) =>
            contract.Done.SelectMany((port, slot) => run.Firings[dataOut.Count + slot].Select(firing => (port.Name, firing))).ToList();

        // The first done to fire, or null when none did.
        public (string Port, Firing Firing)? FirstDone(PortRun run) =>
            Dones(run).OrderBy(done => done.Firing.Step).Cast<(string, Firing)?>().FirstOrDefault();

        // The step the computation's first done fired on, counted from the step start reaches the part; null when none fired.
        public int? Latency(PortRun run, int caseIndex) => FirstDone(run)?.Firing.Step - (startSteps[caseIndex] + 1);

        // A data out-port's firings after start, in step order.
        public List<Firing> AfterStart(PortRun run, int slot, int caseIndex) => run.Firings[slot].Where(firing => firing.Step > startSteps[caseIndex]).ToList();

        // The value a data out-port carries given the step done fired on; null when it is no value of its kind.
        public static int? Value(Port port, List<Firing> firingsAfterStart, int done) =>
            port.Kind == PortKind.Binary ? BinaryWord(port.Width, firingsAfterStart, done) : UnaryValue(port.Kind, firingsAfterStart, done);

        // What one computation of the case read: the done ports that fired, then each data out-port's value ("?" when it is no value).
        public string Reading(PortRun run, int caseIndex)
        {
            List<(string Port, Firing Firing)> dones = Dones(run).OrderBy(done => done.Firing.Step).ToList();
            int done = dones.Count == 0 ? int.MaxValue : dones[0].Firing.Step;
            IEnumerable<string> values = dataOut.Select((port, slot) => $"{port.Name}={Value(port, AfterStart(run, slot, caseIndex), done)?.ToString() ?? "?"}");
            return string.Join(",", dones.Select(fired => fired.Port).DefaultIfEmpty("no done").Concat(values));
        }

        // Each trigger out-port's first firing after start comes on a later step than the one listed before it.
        public bool TriggersInOrder(PortRun run, int caseIndex)
        {
            List<int> firstSteps = dataOut.Select((port, slot) => (port, slot))
                .Where(pair => pair.port.Kind == PortKind.Trigger)
                .Select(pair => AfterStart(run, pair.slot, caseIndex).Select(firing => firing.Step).DefaultIfEmpty(-1).First())
                .Where(step => step >= 0)
                .ToList();
            return firstSteps.Zip(firstSteps.Skip(1)).All(pair => pair.First < pair.Second);
        }

        // Null when the word spills outside its window after done.
        private static int? BinaryWord(int width, List<Firing> firingsAfterStart, int done) =>
            firingsAfterStart.Any(firing => firing.Step < done || firing.Step >= done + width)
                ? null
                : firingsAfterStart.Aggregate(0, (bits, firing) => bits | 1 << (firing.Step - done));

        // Null when the port fires at or after done, or its firings are no value of its kind.
        private static int? UnaryValue(PortKind kind, List<Firing> firingsAfterStart, int done)
        {
            if (firingsAfterStart.Any(firing => firing.Step >= done))
            {
                return null;
            }
            return kind switch
            {
                PortKind.Count => (int)firingsAfterStart.Sum(firing => firing.Spikes),
                PortKind.Interval => firingsAfterStart.Count == 2 ? firingsAfterStart[1].Step - firingsAfterStart[0].Step : null,
                _ => firingsAfterStart.Count <= 1 ? firingsAfterStart.Count : null,
            };
        }
    }
}
