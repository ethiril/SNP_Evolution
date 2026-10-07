using System.Linq;

namespace SnpEvolution.Simulation
{
    // What an engine can run: jitter, axonal delays, Ports readouts, whether it follows every computation of a
    // nondeterministic network rather than sampling, and the largest output neuron count, rule delay and spikes sent
    // it can hold. A caller checks trials against it once (see RoutedEngine), and an engine handed a trial it cannot run
    // returns TrialResult.Unsupported for it.
    public sealed record EngineSupport(bool Jitter, bool AxonalDelay, bool Ports, bool EveryComputation,
        int MaxOutputNeurons = int.MaxValue, int MaxDelay = int.MaxValue, int MaxSpikesSent = int.MaxValue)
    {
        // A sampling engine on the CPU, which runs anything.
        public static readonly EngineSupport Sampling = new EngineSupport(Jitter: true, AxonalDelay: true, Ports: true, EveryComputation: false);

        public bool Runs(Trial trial, SimulationOptions options)
        {
            CompiledNetwork network = CompiledNetwork.Of(trial.Network);
            return (Jitter || options.Jitter == 0)
                && (Ports || trial.Readout != Readout.Ports)
                && (AxonalDelay || network.MaxAxonalDelay == 0)
                && network.isOutput.Count(output => output) <= MaxOutputNeurons
                && network.ruleDelay.All(delay => delay <= MaxDelay)
                && network.ruleProduce.All(produce => produce <= MaxSpikesSent);
        }
    }
}
