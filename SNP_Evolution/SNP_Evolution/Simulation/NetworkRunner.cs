using System;
using System.Collections.Generic;
using SnpEvolution.Networks;

namespace SnpEvolution.Simulation
{
    public static class NetworkRunner
    {
        private const int SilentRunsBeforeGivingUp = 7;

        public static int? RunOnce(Network network, int maxSteps, Random random)
        {
            var simulation = new NetworkSimulation(network, random);
            while (simulation.Output == null && simulation.StepCount < maxSteps)
            {
                simulation.Step();
            }
            return simulation.Output;
        }

        public static List<int> CollectOutputs(Network network, int maxSteps, int repetitions, Random random)
        {
            var outputs = new List<int>();
            for (int run = 0; run < repetitions; run++)
            {
                if (RunOnce(network, maxSteps, random) is int output)
                {
                    outputs.Add(output);
                }
                if (outputs.Count == 0 && run + 1 >= SilentRunsBeforeGivingUp)
                {
                    break;
                }
            }
            outputs.Sort();
            return outputs;
        }
    }
}
