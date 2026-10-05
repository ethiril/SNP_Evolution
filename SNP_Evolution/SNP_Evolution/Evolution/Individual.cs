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

        public string Description { get; private set; } = "";

        public bool Exact { get; private set; }

        public bool IsEvaluated { get; private set; }

        // The task's behaviour cell for this network, if the task has one.
        public (int, int)? Niche { get; private set; }

        public void Record(FitnessResult result)
        {
            Fitness = result.Fitness;
            Outputs = result.Outputs;
            Description = result.Description;
            Exact = result.Exact;
            Niche = result.Niche;
            IsEvaluated = true;
        }
    }
}
