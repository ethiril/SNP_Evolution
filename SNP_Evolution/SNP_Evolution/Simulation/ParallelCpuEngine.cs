using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SnpEvolution.Networks;

namespace SnpEvolution.Simulation
{
    // Runs the networks across all cores. Random is not thread-safe, so each network gets its own generator,
    // seeded up front from the given random so the results do not depend on thread scheduling.
    public sealed class ParallelCpuEngine : ISimulationEngine
    {
        public IReadOnlyList<IReadOnlyList<int>> CollectOutputs(IReadOnlyList<Network> networks, SimulationOptions options, Random random)
        {
            var seeds = new int[networks.Count];
            for (int index = 0; index < seeds.Length; index++)
            {
                seeds[index] = random.Next();
            }
            var outputs = new IReadOnlyList<int>[networks.Count];
            Parallel.For(0, networks.Count, index =>
                outputs[index] = NetworkRunner.CollectOutputs(networks[index], options.MaxSteps, options.Repetitions, new Random(seeds[index])));
            return outputs;
        }
    }
}
