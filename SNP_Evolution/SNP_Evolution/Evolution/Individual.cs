using System;
using System.Collections.Generic;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution
{
    public sealed class Individual
    {
        public Individual(Network genes)
        {
            Genes = genes;
        }

        public Network Genes { get; }

        public float Fitness { get; private set; }

        public IReadOnlyList<int> Outputs { get; private set; } = Array.Empty<int>();

        public void Record(FitnessResult result)
        {
            Fitness = result.Fitness;
            Outputs = result.Outputs;
        }
    }
}
