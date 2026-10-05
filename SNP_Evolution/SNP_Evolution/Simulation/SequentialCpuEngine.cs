using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Simulation
{
    // Samples every trial on the calling thread, drawing straight from the given random.
    public sealed class SequentialCpuEngine : ISimulationEngine
    {
        public IReadOnlyList<TrialResult> Run(IReadOnlyList<Trial> trials, SimulationOptions options, Random random) =>
            trials.Select(trial => NetworkRunner.Sample(trial, options, random)).ToList();
    }
}
