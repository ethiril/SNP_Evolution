using System;
using System.Collections.Generic;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Operators
{
    public sealed record GeneticOperators(IParentSelection Selection, ICrossover Crossover, IMutation Mutation);

    public interface IParentSelection
    {
        // Called once per generation with the population ranked fittest first; the returned function picks one parent per call.
        Func<Individual> Prepare(IReadOnlyList<Individual> ranked, Random random);
    }

    public interface ICrossover
    {
        Network Cross(Network firstParent, Network secondParent, Random random);
    }

    public interface IMutation
    {
        // Decides for itself whether to mutate, and returns the network unchanged when it does not.
        Network Mutate(Network network, Random random);
    }
}
