using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Operators
{
    // Puts what another operator makes back within the profile, so every child of a profile run fits it.
    public sealed class ProfileMutation : IMutation
    {
        private readonly IMutation inner;

        public ProfileMutation(IMutation inner) => this.inner = inner;

        public Network Mutate(Network network, Random random) => HardwareProfile.Conform(inner.Mutate(network, random));
    }

    public sealed class ProfileCrossover : ICrossover
    {
        private readonly ICrossover inner;

        public ProfileCrossover(ICrossover inner) => this.inner = inner;

        public Network Cross(Network firstParent, Network secondParent, Random random) => HardwareProfile.Conform(inner.Cross(firstParent, secondParent, random));
    }
}
