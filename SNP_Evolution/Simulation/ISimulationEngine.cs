using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Model;

namespace SnpEvolution.Simulation
{
    // A backend that runs networks. Taking the whole batch lets a backend spread the runs over cores or a GPU.
    public interface ISimulationEngine
    {
        EngineSupport Support { get; }

        // Returns one result per trial, in order. Engines draw any randomness from random, or seed their own
        // generators from it, so a seeded random makes a batch reproducible.
        IReadOnlyList<TrialResult> Run(IReadOnlyList<Trial> trials, SimulationOptions options, Random random);
    }

    public static class SimulationEngineExtensions
    {
        // Each network's sorted outputs when it runs as a generator, with no input.
        public static IReadOnlyList<IReadOnlyList<int>> CollectOutputs(
            this ISimulationEngine engine, IReadOnlyList<Network> networks, SimulationOptions options, Random random) =>
            engine.Run(networks.Select(Trial.Generate).ToList(), options, random).Select(result => result.Outputs).ToList();
    }
}
