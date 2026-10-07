using System;
using SnpEvolution.Simulation;

namespace SnpEvolution.Application
{
    // The engine benchmarks and evolve-parts run on: exhaustive unless sampled, which follows one random choice per run.
    // MaxConfigurations caps how many configurations the exhaustive engine follows at once.
    public sealed record EngineChoice(bool Sampled = false, int MaxConfigurations = ExhaustiveCpuEngine.DefaultMaxConfigurations)
    {
        public Func<ISimulationEngine> Factory => Sampled ? () => new SequentialCpuEngine() : () => new ExhaustiveCpuEngine(MaxConfigurations);

        // The exhaustive engine is the default, so it goes unsaid.
        public string CommandLineFlag => Sampled ? " --engine sampled" : "";
    }
}
