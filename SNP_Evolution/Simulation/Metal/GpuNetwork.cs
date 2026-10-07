using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SnpEvolution.Simulation.Metal
{
    // The layouts below match the structs of the same names in Simulation.metal field for field.
    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuNeuron
    {
        public long InitialSpikes;
        public uint RuleBegin;
        public uint RuleEnd;
        public uint IncomingBegin;
        public uint IncomingEnd;
        public int InputOrdinal;
        public int OutputOrdinal;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct GpuRule
    {
        public long Consume;
        public ulong PeriodMagic;
        public uint AcceptStart;
        public int Tail;
        public int Period;
        public int Produce;
        public int Delay;
        public uint Fires;
    }

    // A compiled network laid out for the kernel. Indexes are local to the network, and each neuron lists the neurons
    // that send to it, because the kernel gathers spikes instead of scattering them.
    internal sealed class GpuNetwork
    {
        // The kernel's limits, which MetalEngine.Support declares: it keeps output releases in a fixed table, and delays
        // and emissions in 16 bits.
        public const int MaxOutputs = 64;
        public const int MaxSmallValue = ushort.MaxValue;

        private static readonly ConditionalWeakTable<CompiledNetwork, GpuNetwork> Cache = new ConditionalWeakTable<CompiledNetwork, GpuNetwork>();

        private GpuNetwork(CompiledNetwork network)
        {
            int count = network.NeuronCount;
            var incomingCount = new int[count + 1];
            foreach (int receiver in network.targets)
            {
                incomingCount[receiver + 1]++;
            }
            for (int neuron = 0; neuron < count; neuron++)
            {
                incomingCount[neuron + 1] += incomingCount[neuron];
            }
            Incoming = new uint[network.targets.Length];
            var filled = new int[count];
            for (int sender = 0; sender < count; sender++)
            {
                for (int target = network.targetStart[sender]; target < network.targetStart[sender + 1]; target++)
                {
                    int receiver = network.targets[target];
                    Incoming[incomingCount[receiver] + filled[receiver]++] = (uint)sender;
                }
            }

            var inputOrdinal = new int[count];
            Array.Fill(inputOrdinal, -1);
            for (int ordinal = 0; ordinal < network.inputNeurons.Length; ordinal++)
            {
                inputOrdinal[network.inputNeurons[ordinal]] = ordinal;
            }

            Neurons = new GpuNeuron[count];
            int outputCount = 0;
            for (int neuron = 0; neuron < count; neuron++)
            {
                Neurons[neuron] = new GpuNeuron
                {
                    InitialSpikes = network.initialSpikes[neuron],
                    RuleBegin = (uint)network.ruleStart[neuron],
                    RuleEnd = (uint)network.ruleStart[neuron + 1],
                    IncomingBegin = (uint)incomingCount[neuron],
                    IncomingEnd = (uint)incomingCount[neuron + 1],
                    InputOrdinal = inputOrdinal[neuron],
                    OutputOrdinal = network.isOutput[neuron] ? outputCount++ : -1,
                };
            }
            OutputCount = outputCount;

            Rules = new GpuRule[network.ruleDelay.Length];
            for (int rule = 0; rule < Rules.Length; rule++)
            {
                Rules[rule] = new GpuRule
                {
                    Consume = network.ruleConsume[rule],
                    PeriodMagic = ulong.MaxValue / (ulong)network.acceptPeriod[rule] + 1,
                    AcceptStart = (uint)network.acceptStart[rule],
                    Tail = network.acceptTail[rule],
                    Period = network.acceptPeriod[rule],
                    Produce = network.ruleProduce[rule],
                    Delay = network.ruleDelay[rule],
                    Fires = network.ruleFires[rule] ? 1u : 0u,
                };
            }
            Accepts = new byte[network.accepts.Length];
            for (int index = 0; index < Accepts.Length; index++)
            {
                Accepts[index] = network.accepts[index] ? (byte)1 : (byte)0;
            }
        }

        public GpuNeuron[] Neurons { get; }

        public GpuRule[] Rules { get; }

        public byte[] Accepts { get; }

        // For each neuron in turn, the senders of its incoming synapses; GpuNeuron.IncomingBegin and End index into it.
        public uint[] Incoming { get; }

        public int OutputCount { get; }

        public static GpuNetwork Of(CompiledNetwork network) => Cache.GetValue(network, created => new GpuNetwork(created));
    }
}
