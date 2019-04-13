using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SNP_First_Test.Utilities;

namespace SNP_First_Test.Network
{
    /* 
     *   A network is made up of multiple neurons, this can be edited to contain as many or as little
     *   neurons as you would like. 
     */
    public class Network
    {
        public List<Neuron> Neurons { get; set; }
        public List<int> OutputSet { get; set; }
        public int CurrentOutput { get; set; }
        public bool IsClear { get; set; }
        public int GlobalTimer { get; set; }
        public bool IsEngaged { get; set; }

        public Network(List<Neuron> neurons)
        {
            Neurons = neurons;
            OutputSet = new List<int>() { };
            CurrentOutput = 0;
            IsClear = false;
            IsEngaged = false;
        }

        /// <summary>
        /// Execute a step in the network
        /// </summary>
        /// <param name="networkRef">Network which will be executed</param>
        public void Spike(Network networkRef)
        {
            /*
             * Run through removing all of the spikes and recording an output
             * then run through adding all of the spikes that are recorded to have a spike go out to their network
             * Check if each spike sends spike across all axons or if it determines which to go through randomly.
             * Add needs to be done on a copy of the network BEFORE the removal happens (original network)
             */
            List<Neuron> NeuronCopy = new List<Neuron>(this.Neurons);
            List<Neuron> NeuronAdditionCopy = ReflectionCloner.DeepFieldClone(this.Neurons);
            Parallel.ForEach(NeuronCopy, neuron =>
            {
                if (neuron.RemoveSpikes(networkRef) == true)
                {
                    if (neuron.IsOutput == true && this.IsEngaged == true)
                    {
                        this.OutputSet.Add(++this.CurrentOutput);
                        this.IsClear = true;
                    }
                    else if (neuron.IsOutput == true && this.IsEngaged == false)
                    {
                        this.IsEngaged = true;
                    }
                }
                else
                {
                    if (neuron.IsOutput == true)
                    {
                        this.CurrentOutput++;
                    }
                }
            });
            Parallel.ForEach(NeuronAdditionCopy, neuron =>
            {
                if (neuron.ActiveDelay == 0)
                {
                    neuron.FireSpike(networkRef, neuron.Connections);
                }
            });
            this.GlobalTimer++;
        }

        /// <summary>
        /// Method to print each rule across the network
        /// </summary>
        /// <param name="neurons"></param>
        private void PrintNetwork(List<Neuron> neurons)
        {
            int count = 0;
            foreach (Neuron neuron in neurons)
            {
                count++;
                foreach (Rule rule in neuron.Rules) { Console.Write(rule.RuleExpression + "->" + rule.Fire + ";" + rule.Delay + ", "); };
            }
        }

        /// <summary>
        /// Smaller, more compact, pretty version of print();
        /// </summary>
        public void minifiedPrint()
        {
            int count = 0;
            Console.WriteLine("Network breakdown");
            foreach (Neuron neuron in this.Neurons)
            {
                count++;
                Console.WriteLine("Neuron: {0}, Current Spikes: {1}, Rule Amount: {2}, Current Rules: ",+count, neuron.SpikeCount, neuron.Rules.Count);
                foreach (Rule rule in neuron.Rules) { Console.Write(rule.RuleExpression + " -> " + rule.Fire + ";" + rule.Delay + ", "); };
                Console.Write("Neuron connections: ");
                foreach (int connection in neuron.Connections) { Console.Write(connection + ", "); };
                Console.WriteLine("Is output neuron: " + neuron.IsOutput);
            }
        }

        /// <summary>
        /// Full print of the network
        /// </summary>
        public void print()
        {
            int count = 0;
            Console.WriteLine("Network printout: ");
            Console.WriteLine("Neuron amount: " + this.Neurons.Count);
            Console.WriteLine("Network breakdown");
            foreach (Neuron neuron in this.Neurons)
            {
                count++;
                Console.WriteLine("Neuron: " + count);
                Console.WriteLine("Current spikes: " + neuron.SpikeCount);
                Console.WriteLine("Rule amount: " + neuron.Rules.Count);
                Console.Write("Current Rules: ");
                foreach (Rule rule in neuron.Rules) { Console.Write(rule.RuleExpression + " -> " + rule.Fire + ";" + rule.Delay + ", "); };
                Console.WriteLine();
                Console.Write("Neuron connections: ");
                foreach (int connection in neuron.Connections) { Console.Write(connection + ", "); };
                Console.WriteLine("Is output neuron: " + neuron.IsOutput);
                Console.WriteLine("\n");

            }
        }

        // Reflection cloner library thanks to http://blog.nuclex-games.com/mono-dotnet/fast-deep-cloning/ (ClonerHelpers.cs, ICloneFactory.cs, ReflectionCloner.cs)

    }
}
