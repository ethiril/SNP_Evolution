using System;
using SnpEvolution.Simulation;

namespace SnpEvolution.Application
{
    // The engine benchmarks and evolve-parts run on: exhaustive unless sampled, which follows one random choice per run.
    // MaxConfigurations caps how many configurations the exhaustive engine follows at once.
    internal sealed record EngineChoice(bool Sampled = false, int MaxConfigurations = ExhaustiveCpuEngine.DefaultMaxConfigurations)
    {
        public Func<ISimulationEngine> Factory => Sampled ? () => new SequentialCpuEngine() : () => new ExhaustiveCpuEngine(MaxConfigurations);

        // How a part's origin records the engine it was evolved on; the exhaustive engine is the default and goes unsaid.
        public string Option => Sampled ? " --engine sampled" : "";
    }
}
