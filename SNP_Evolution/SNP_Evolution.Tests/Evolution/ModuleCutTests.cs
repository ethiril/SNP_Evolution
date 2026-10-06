using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Networks;
using static SnpEvolution.Tests.Evolution.ModuleFixtures;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Evolution
{
    public class ModuleCutTests
    {
        [Fact]
        public void CutKeepsInsideSynapsesAndFindsThePorts()
        {
            Cut cut = ModuleCuts.Cut(Chain(), new[] { 1, 2 });

            Assert.Equal(2, cut.Body.Neurons.Count);
            Assert.Equal(new[] { 2 }, cut.Body.Neurons[0].Connections);
            Assert.True(cut.Body.Neurons[1].IsOutput);
            Assert.Equal(new[] { 0 }, cut.Inputs);
            Assert.Equal(new[] { 1 }, cut.Outputs);
        }

        [Fact]
        public void PruningDropsNeuronsThatCannotReachTheOutput()
        {
            Network network = new Network(Chain().Neurons.Append(Neuron(3, new[] { 5 }, Standard("a", 1))).Append(Neuron(0, new[] { 4 }, Standard("a", 1))).ToList());

            Network pruned = ModuleCuts.Prune(network);

            Assert.Equal(3, pruned.Neurons.Count);
            Assert.Equal(Chain().Neurons.Select(neuron => neuron.Connections), pruned.Neurons.Select(neuron => neuron.Connections));
        }

        [Fact]
        public void ChangedNeuronsAreFoundWithOrWithoutRenumbering()
        {
            Network parent = Chain();
            Network nudged = parent.WithNeuron(1, parent.Neurons[1].WithInitialSpikes(2));
            Network grown = NetworkEdits.AddNeuron(parent, Neuron(1, new[] { 3 }, Standard("a", 1)));

            Assert.Equal(new[] { 1 }, ModuleCuts.Changed(parent, nudged));
            Assert.Equal(new[] { 3 }, ModuleCuts.Changed(parent, grown));
            Assert.Equal(new[] { 1, 0, 2 }, ModuleCuts.AroundChanges(nudged, new[] { 1 }, 6));
        }
    }
}
