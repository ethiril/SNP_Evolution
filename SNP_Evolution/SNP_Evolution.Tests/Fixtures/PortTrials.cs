using SnpEvolution.Model;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Fixtures
{
    internal static class PortTrials
    {
        public static readonly SimulationOptions Options = new SimulationOptions(MaxSteps: 20, Repetitions: 3);

        // Neuron 2 sends two spikes to 3 and 3 one back to 2 on alternate steps for ever, so a run ends only through its watch.
        public static Network Loop() => new Network(new[]
        {
            Neuron(1, new[] { 2 }, Standard("a", 1)),
            Neuron(0, new[] { 3 }, Standard("a", 1, produce: 2)),
            Neuron(0, new[] { 2 }, Standard("aa", 2)),
        });

        // With 3 as done, done fires on step 2, so steps 0 to 3 run and 3 ends holding the two spikes 2 sent on step 3.
        public static Trial LoopTrial() => new Trial(Loop(), InputSpikes.None, Readout.Ports, new PortWatch(new[] { 2, 3 }, new[] { 3 }, StepsAfterDone: 1));
    }
}
