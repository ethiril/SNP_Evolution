using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Evolution
{
    internal static class ModuleFixtures
    {
        internal static Individual Scored(Network network, float fitness, params float[] checks)
        {
            var individual = new Individual(network);
            individual.Record(new FitnessResult(fitness, Array.Empty<int>(), Checks: checks));
            return individual;
        }

        // n1 -> n2 -> n3 (out), with n2 waiting a step.
        internal static Network Chain() => new Network(new[]
        {
            Neuron(1, new[] { 2 }, Standard("a", 1)),
            Neuron(0, new[] { 3 }, Standard("a", 1, delay: 1)),
            OutputNeuron(0, Standard("a", 1)),
        });

        // A part checked against its contract on the exhaustive engine and recorded as a library part would be.
        internal static LibraryPart Verified(Part part)
        {
            PartMeasurement measurement = PartEvolution.Measure(part);
            Assert.True(measurement.MeetsContract, measurement.Description);
            return LibraryPart.Of(part, measurement, new PartOrigin(1, "a test", 0));
        }

        internal static Module ModuleOf(ModuleLibrary library, Network network) => library.Add(ModuleCuts.Whole(network), "a test")!;

        // The input passes the trigger on, n2 waits a step, and the output and n4 then pass a spike back and forth.
        internal static Network TriggeredTwos() => new Network(new[]
        {
            InputNeuron(new[] { 2 }, Standard("a", 1)),
            Neuron(0, new[] { 3 }, Standard("a", 1)),
            new Neuron(new[] { Standard("a", 1) }, 0, new[] { 4 }, true),
            Neuron(0, new[] { 3 }, Standard("a", 1)),
        });
    }
}
