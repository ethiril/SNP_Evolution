using System;
using System.Collections.Generic;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Genome;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Search
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

        // Under the hardware profile, what an operator or the starting networks make is put back within it, so no edit
        // has to know the profile; outside it they are returned as given.
        public IMutation Conformed(IMutation mutation) => Factory.Space.HardwareProfile ? new ProfileMutation(mutation) : mutation;

        public ICrossover Conformed(ICrossover crossover) => Factory.Space.HardwareProfile ? new ProfileCrossover(crossover) : crossover;

        public Func<Network> Conformed(Func<Network> create) => Factory.Space.HardwareProfile ? () => HardwareProfile.Conform(create()) : create;

        public IParentSelection Selection(IParentSelection usual) => Lexicase ? new LexicaseSelection(usual) : usual;
    }
}
