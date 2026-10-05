using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Networks;

namespace SnpEvolution.Simulation
{
    // Runs every network on the calling thread, drawing straight from the given random.
    public sealed class SequentialCpuEngine : ISimulationEngine
    {
        public IReadOnlyList<IReadOnlyList<int>> CollectOutputs(IReadOnlyList<Network> networks, SimulationOptions options, Random random) =>
            networks.Select(network => NetworkRunner.CollectOutputs(network, options.MaxSteps, options.Repetitions, random)).ToList();
    }
}
