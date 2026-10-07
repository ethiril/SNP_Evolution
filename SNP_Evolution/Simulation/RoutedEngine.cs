using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Simulation
{
    // Picks an engine for each trial from what the engines declare: the preferred engine runs every trial its
    // EngineSupport allows, and the general one, which should run anything, takes the rest. A batch with less work than
    // minimumWork, in neuron-steps (every trial's neurons times MaxSteps times Repetitions), goes wholly to the general
    // engine with the caller's random, for a preferred engine such as the GPU whose round trip only pays on large batches.
    public sealed class RoutedEngine : ISimulationEngine
    {
        private readonly ISimulationEngine preferred;
        private readonly ISimulationEngine general;
        private readonly long minimumWork;

        public RoutedEngine(ISimulationEngine preferred, ISimulationEngine general, long minimumWork = 0)
        {
            this.preferred = preferred;
            this.general = general;
            this.minimumWork = minimumWork;
        }

        public EngineSupport Support => general.Support with { EveryComputation = preferred.Support.EveryComputation && general.Support.EveryComputation };

        public IReadOnlyList<TrialResult> Run(IReadOnlyList<Trial> trials, SimulationOptions options, Random random)
        {
            long work = trials.Sum(trial => (long)CompiledNetwork.Of(trial.Network).NeuronCount) * options.MaxSteps * options.Repetitions;
            if (work < minimumWork)
            {
                return general.Run(trials, options, random);
            }
            List<int> onPreferred = Enumerable.Range(0, trials.Count).Where(index => preferred.Support.Runs(trials[index], options)).ToList();
            if (onPreferred.Count == trials.Count)
            {
                return preferred.Run(trials, options, random);
            }
            if (onPreferred.Count == 0)
            {
                return general.Run(trials, options, random);
            }
            List<int> onGeneral = Enumerable.Range(0, trials.Count).Except(onPreferred).ToList();
            var results = new TrialResult[trials.Count];
            Fill(preferred, onPreferred, new Random(random.Next()));
            Fill(general, onGeneral, new Random(random.Next()));
            return results;

            void Fill(ISimulationEngine engine, List<int> indexes, Random engineRandom)
            {
                IReadOnlyList<TrialResult> run = engine.Run(indexes.Select(index => trials[index]).ToList(), options, engineRandom);
                for (int position = 0; position < indexes.Count; position++)
                {
                    results[indexes[position]] = run[position];
                }
            }
        }
    }
}
