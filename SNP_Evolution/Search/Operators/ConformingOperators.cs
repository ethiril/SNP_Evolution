using System;
using SnpEvolution.Model;
using SnpEvolution.Search.Genome;

namespace SnpEvolution.Search.Operators
{
    // Keeps what another operator makes within the genome space: a child is put back within the profile, and one that
    // leaves a deterministic space is made again, up to Attempts times, before the first parent is kept unchanged.
    // Retrying rather than dropping a rival rule keeps edits from deleting rules the search may still need.
    public sealed class ConformingMutation : IMutation
    {
        public const int Attempts = 8;

        private readonly IMutation inner;
        private readonly GenomeSpace space;

        public ConformingMutation(IMutation inner, GenomeSpace space)
        {
            this.inner = inner;
            this.space = space;
        }

        public Network Mutate(Network network, Random random)
        {
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                Network child = space.Profiled(inner.Mutate(network, random));
                if (space.Fits(child))
                {
                    return child;
                }
            }
            return network;
        }
    }

    public sealed class ConformingCrossover : ICrossover
    {
        private readonly ICrossover inner;
        private readonly GenomeSpace space;

        public ConformingCrossover(ICrossover inner, GenomeSpace space)
        {
            this.inner = inner;
            this.space = space;
        }

        public Network Cross(Network firstParent, Network secondParent, Random random)
        {
            for (int attempt = 0; attempt < ConformingMutation.Attempts; attempt++)
            {
                Network child = space.Profiled(inner.Cross(firstParent, secondParent, random));
                if (space.Fits(child))
                {
                    return child;
                }
            }
            return firstParent;
        }
    }
}
