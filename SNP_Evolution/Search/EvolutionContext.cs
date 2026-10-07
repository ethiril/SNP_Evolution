using System;
using System.Collections.Generic;
using SnpEvolution.Model;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Genome;
using SnpEvolution.Search.Modules;
using SnpEvolution.Search.Operators;
using SnpEvolution.Specs.Parts;

namespace SnpEvolution.Search
{
    // Everything an algorithm needs to start evolving, independent of which algorithm it is. Lexicase makes parents
    // be picked by lexicase selection where the algorithm picks parents; Modules lets mutation use a module library.
    public sealed record EvolutionContext(
        int PopulationSize,
        float MutationRate,
        Random Random,
        Func<Network> CreateStartingNetwork,
        IPopulationEvaluator Evaluator,
        NetworkFactory Factory,
        Action<string> Log,
        MutationPressure? Pressure = null,
        bool Lexicase = false,
        ModuleSupport? Modules = null,
        ModuleLibrary? Parts = null,
        CompositionMix? Composition = null)
    {
        public WeightedMutation StructuralMutation(float rate) => WeightedMutation.Structural(rate, Factory, Pressure, Modules);

        // Under the hardware profile or a deterministic space, what an operator or the starting networks make is put back
        // within the space, so no edit has to know it; otherwise they are returned as given.
        public IMutation Conformed(IMutation mutation) => Factory.Space.Conforms ? new ConformingMutation(mutation, Factory.Space) : mutation;

        public ICrossover Conformed(ICrossover crossover) => Factory.Space.Conforms ? new ConformingCrossover(crossover, Factory.Space) : crossover;

        public Func<Network> Conformed(Func<Network> create) => Factory.Space.Conforms ? () => Factory.Space.Conform(create()) : create;

        public IParentSelection Selection(IParentSelection usual) => Lexicase ? new LexicaseSelection(usual) : usual;
    }
}
