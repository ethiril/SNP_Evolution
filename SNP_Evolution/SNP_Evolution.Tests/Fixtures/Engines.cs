using SnpEvolution.Simulation;

namespace SnpEvolution.Tests.Fixtures
{
    // The CPU engines, for theories that run the same trials on each.
    public static class Engines
    {
        public static TheoryData<ISimulationEngine> Sampling => new TheoryData<ISimulationEngine> { new SequentialCpuEngine(), new ParallelCpuEngine() };

        public static TheoryData<ISimulationEngine> Cpu => new TheoryData<ISimulationEngine> { new SequentialCpuEngine(), new ParallelCpuEngine(), new ExhaustiveCpuEngine() };
    }
}
