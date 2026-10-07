using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Algorithms
{
    // Newcomers a stalled search is given, waiting for the next generation. An algorithm that breeds a batch puts them
    // first in it; one that keeps a ranked population waiting puts them in place of its weakest members.
    public sealed class Immigrants
    {
        private List<Network> waiting = new List<Network>();

        public void Arrive(IReadOnlyList<Network> newcomers) => waiting = newcomers.ToList();

        // A batch of the given size: the newcomers that fit, then children until it is full. The newcomers are used up.
        public List<Individual> Batch(int size, Func<Network> child)
        {
            List<Individual> batch = waiting.Take(size).Select(network => new Individual(network))
                .Concat(Enumerable.Range(0, Math.Max(0, size - waiting.Count)).Select(_ => new Individual(child())))
                .ToList();
            waiting = new List<Network>();
            return batch;
        }

        // The ranked population waiting to be scored with newcomers in place of its last members, keeping at least the
        // first few.
        public static List<Individual> ReplaceWeakest(IReadOnlyList<Individual> ranked, IReadOnlyList<Network> newcomers, int keepAtLeast)
        {
            int keep = Math.Max(keepAtLeast, ranked.Count - newcomers.Count);
            return ranked.Take(keep).Concat(newcomers.Take(ranked.Count - keep).Select(network => new Individual(network))).ToList();
        }

        // Newcomers made from the best scored networks, such as copies with a new part wired in: the hosts are taken in
        // turn, from the best, until enough are made or the attempts run out. make gives null when it cannot make one
        // from a host.
        public static List<Network> FromBest(IEnumerable<Individual> population, int hosts, int wanted, int attempts, Func<Network, Network?> make)
        {
            List<Individual> best = Ranking.Rank(population.Where(individual => individual.IsEvaluated)).Take(hosts).ToList();
            var made = new List<Network>();
            for (int attempt = 0; best.Count > 0 && attempt < attempts && made.Count < wanted; attempt++)
            {
                if (make(best[attempt % best.Count].Genes) is Network newcomer)
                {
                    made.Add(newcomer);
                }
            }
            return made;
        }
    }
}
