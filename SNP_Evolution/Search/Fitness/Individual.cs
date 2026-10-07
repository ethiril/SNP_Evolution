using System;
using System.Collections.Generic;
using SnpEvolution.Model;

namespace SnpEvolution.Search.Fitness
{
    // A candidate with a score: what ranking and lexicase selection compare, whether it is a network or a program.
    // Size breaks ties between equally fit candidates, smaller first.
    public interface IScored
    {
        float Fitness { get; }

        int Size { get; }

        // The score, from 0 to 1, on each separate thing the task checks; empty when the task does not say.
        IReadOnlyList<float> Checks { get; }
    }

    public sealed class Individual : IScored
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

        public int Size => Genes.Size;

        // The task's behaviour cell for this network, if the task has one.
        public (int, int)? Niche { get; private set; }

        // The score, from 0 to 1, on each separate thing the task checks; empty when the task does not say.
        public IReadOnlyList<float> Checks { get; private set; } = Array.Empty<float>();

        public void Record(FitnessResult result)
        {
            Fitness = result.Fitness;
            Outputs = result.Outputs;
            Description = result.Description;
            Exact = result.Exact;
            Niche = result.Niche;
            Checks = result.Checks ?? Array.Empty<float>();
            IsEvaluated = true;
        }
    }
}
