using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Proposals;
using SnpEvolution.Evolution.Tasks;

namespace SnpEvolution.Application
{
    // A run is the algorithm wrapped in stages, modules, proposals and stagnation recovery; these find the layers inside.
    internal static class RunLayers
    {
        // True when the whole target was matched, rather than only an early stage of it.
        public static bool IsSolved(IGeneticAlgorithm geneticAlgorithm, ITask? task = null) =>
            geneticAlgorithm is IterativeEvolution iterative
                ? iterative.IsComplete
                : geneticAlgorithm.Best is Individual best && Solved.Solves(best.Fitness, task);

        // The algorithm doing the work, without the stages, modules and stagnation recovery around it.
        public static IGeneticAlgorithm Unwrap(IGeneticAlgorithm geneticAlgorithm) => geneticAlgorithm switch
        {
            IterativeEvolution iterative => Unwrap(iterative.Algorithm),
            StagnationRecovery recovery => Unwrap(recovery.Inner),
            ModularEvolution modular => Unwrap(modular.Inner),
            PartProposals proposals => Unwrap(proposals.Inner),
            _ => geneticAlgorithm,
        };

        public static PartProposals? Proposals(IGeneticAlgorithm geneticAlgorithm) => geneticAlgorithm switch
        {
            PartProposals proposals => proposals,
            IterativeEvolution iterative => Proposals(iterative.Algorithm),
            StagnationRecovery recovery => Proposals(recovery.Inner),
            _ => null,
        };

        // The modular loop somewhere in the run, if it has one.
        public static ModularEvolution? Modular(IGeneticAlgorithm geneticAlgorithm) => geneticAlgorithm switch
        {
            ModularEvolution modular => modular,
            IterativeEvolution iterative => Modular(iterative.Algorithm),
            StagnationRecovery recovery => Modular(recovery.Inner),
            _ => null,
        };
    }
}
