using System;
using System.Collections.Generic;
using SnpEvolution.Networks;

namespace SnpEvolution.Simulation
{
    public sealed record SimulationOptions(int MaxSteps, int Repetitions);

    // A backend that runs networks. Taking the whole batch lets a backend spread the runs over cores or a GPU.
    public interface ISimulationEngine
    {
        // Returns each network's sorted outputs, in the order the networks were given. Engines draw any randomness
        // from random, or seed their own generators from it, so a seeded random makes a batch reproducible.
        IReadOnlyList<IReadOnlyList<int>> CollectOutputs(IReadOnlyList<Network> networks, SimulationOptions options, Random random);
    }
}
