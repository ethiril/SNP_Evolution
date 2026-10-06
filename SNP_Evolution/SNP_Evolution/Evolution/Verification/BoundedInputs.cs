using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;

namespace SnpEvolution.Evolution.Verification
{
    // The inputs a bounded check runs at each bound.
    public static class BoundedInputs
    {
        // Every input whose largest value is exactly the bound, each value within its port's range.
        public static IEnumerable<IReadOnlyDictionary<string, int>> WithLargest(Contract contract, int bound)
        {
            List<Port> ports = contract.DataIn.ToList();
            if (ports.Count == 0)
            {
                return bound == 0 ? new[] { new Dictionary<string, int>() } : Array.Empty<IReadOnlyDictionary<string, int>>();
            }
            IEnumerable<int[]> values = new[] { Array.Empty<int>() };
            foreach (Port port in ports)
            {
                IEnumerable<int> range = Enumerable.Range(Smallest(port), Math.Max(0, Math.Min(bound, Largest(port)) - Smallest(port) + 1));
                values = values.SelectMany(prefix => range.Select(value => prefix.Append(value).ToArray())).ToList();
            }
            return values.Where(row => row.Max() == bound).Select(row => (IReadOnlyDictionary<string, int>)ports.Zip(row).ToDictionary(pair => pair.First.Name, pair => pair.Second));
        }

        // Whether the bound covers every input there is, as it does once every in-port is binary and the bound its largest value.
        public static bool Exhausted(Contract contract, int bound) => contract.DataIn.All(port => port.Kind == PortKind.Binary && Largest(port) <= bound);

        private static int Smallest(Port port) => port.Kind == PortKind.Interval ? 1 : 0;

        private static int Largest(Port port) => port.Kind == PortKind.Binary ? (1 << port.Width) - 1 : int.MaxValue;
    }
}
