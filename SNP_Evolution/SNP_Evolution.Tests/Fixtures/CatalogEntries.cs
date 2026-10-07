using SnpEvolution.Application;
using SnpEvolution.Simulation;

namespace SnpEvolution.Tests.Fixtures
{
    internal static class CatalogEntries
    {
        // One thread keeps a run's evaluation order, and so its counts, the same on every machine.
        public static CatalogEntry<Settings, ISimulationEngine> SingleThreadEngine => Catalog.Engines.Single(engine => engine.Name == "CPU, single thread");
    }
}
