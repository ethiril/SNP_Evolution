using System;
using System.Collections.Generic;
using SNP_Network = SNP_First_Test.Network.Network;

namespace SNP_First_Test.Network
{
    /* 
     *   A neuron is made up of multiple rules and a count of spikes which are currently being held
     *   by the neuron.
     */
    public class Neuron
    {
        // List of rules that will determine whether a neuron will spike
        public List<Rule> Rules { get; set; }
        // Current spike count
        public string SpikeCount { get; set; }
        // List of connections for this neuron
        public List<int> Connections { get; set; }
        // all neurons start with an active delay of 0.
        public int ActiveDelay { get; set; }
        //public string AdditionTemp { get; set; }
        // stored state of the neuron that will get fired after delay is over.
        public bool? PersistedState { get; set; }
        // is this neuron an output neuron
        public bool IsOutput { get; set; }
        private Random random = new Random((int)DateTime.Now.Ticks);



        public Neuron(List<Rule> rules, string spikeCount, List<int> connections, bool isOutput)
        {
            Rules = rules;
            SpikeCount = spikeCount;
            Connections = connections;
            ActiveDelay = 0;
            IsOutput = isOutput;
            PersistedState = false;
        }

        /// <summary>
        /// Determine, randomly, which rule will be chosen
        /// </summary>
        /// <param name="count">amount of rules</param>
        /// <param name="random">random()</param>
        /// <returns>index of rule</returns>
        int DetermineIndex(int count, Random random)
        {
            // if there is just one rule that is fullfilled then the random will always just 0, hence no need to check.
            // otherwise if more rules are matched it will be chosen at random
            return this.random.Next(0, count);
        }

        /// <summary>
        /// Check if any rules match
        /// </summary>
        /// <returns></returns>
        int MatchRules()
        {
            int matchedCount = 0;
            int matchedIndex = 0;
            int count = 0;
            foreach (Rule rule in this.Rules)
            {
                if (rule.IsMatched(this.SpikeCount).Equals(null) || rule.IsMatched(this.SpikeCount).Equals(true))
                {
                    matchedCount++;
                    matchedIndex = count;
                }
                ++count;
            }
            if (matchedCount > 1)
            {
                return DetermineIndex(matchedCount, random);
            }
            else
            {
                return matchedIndex;
            }
        }


        /// <summary>
        /// This code will loop over the entire network and remove any spikes which match the correct rules.
        /// </summary>
        /// <param name="networkRef">the network which this neuron belongs to and for which this method is being executed</param>
        /// <param name="Connections">connections that this neuron has</param>
        /// <returns></returns>
        public bool? RemoveSpikes(SNP_Network networkRef)
        {
            int index;
            if (this.ActiveDelay == 0)
            {
                if (PersistedState.Equals(true))
                {
                    this.SpikeCount = "";
                    PersistedState = false;
                    return true;
                }
                else if (PersistedState.Equals(null))
                {
                    this.SpikeCount = "";
                    PersistedState = false;
                    return null;
                }
                else
                {
                    index = MatchRules();
                    if (this.Rules[index].IsMatched(this.SpikeCount).Equals(null))
                    {
                        if (this.Rules[index].Delay > 0)
                        {
                            this.ActiveDelay = this.Rules[index].Delay;
                            this.PersistedState = null;
                            return false;
                        }
                        this.SpikeCount = "";
                        return null;
                    }
                    else if (this.Rules[index].IsMatched(this.SpikeCount).Equals(false))
                    {
                        return false;
                    }
                    else if (this.Rules[index].IsMatched(this.SpikeCount).Equals(true))
                    {
                        if (this.Rules[index].Delay > 0)
                        {
                            this.ActiveDelay = this.Rules[index].Delay;
                            this.PersistedState = true;
                            return false;
                        }
                        this.SpikeCount = "";
                        return true;
                    }
                }
            }
            else
            {
                this.ActiveDelay--;
                return false;
            }
            // this should never happen.
            Console.Error.WriteLine("Foreach loop failed.");
            return false;
        }

        /// <summary>
        /// Once the spikes have been removed, each network can spike across all of its connections
        /// </summary>
        /// <param name="networkRef">the network which this neuron belongs to and for which this method is being executed</param>
        /// <param name="Connections">connections that this neuron has</param>
        public void FireSpike(SNP_Network networkRef, List<int> Connections)
        {
            /* 
             * If List has more than one rule
             * Check if the neuron has enough spike to satisfy some or all of the rules
             * If only one rule can proceed, complete that spike
             * If more than one rule can proceed at any one time use the Random() function to determine which will fire
             * Fire the spike on that chosen rule definition
             * We do not need to worry about the removal of spikes as that is done in RemoveSpikes()
             */
            int index = MatchRules();
            foreach (int connection in Connections)
            {
                if (this.Rules[index].IsMatched(this.SpikeCount).Equals(true))
                {
                    if (this.Rules[index].Fire)
                    {
                        networkRef.Neurons[connection - 1].SpikeCount = networkRef.Neurons[connection - 1].SpikeCount + "a";
                    }
                }
            }
        }

    }
}
