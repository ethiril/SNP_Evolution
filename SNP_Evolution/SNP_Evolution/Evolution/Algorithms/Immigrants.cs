using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Algorithms
{
    // Newcomers are held until the next generation, so a stall's reaction never disturbs one being scored.
    public sealed class Immigrants
    {
        private List<Network> waiting = new List<Network>();

        public void Arrive(IReadOnlyList<Network> newcomers) => waiting = newcomers.ToList();

        // Newcomers go first and are used up, so each arrives only once.
        public List<Individual> Batch(int size, Func<Network> child)
        {
            List<Individual> batch = waiting.Take(size).Select(network => new Individual(network))
                .Concat(Enumerable.Range(0, Math.Max(0, size - waiting.Count)).Select(_ => new Individual(child())))
                .ToList();
            waiting = new List<Network>();
            return batch;
        }

        // Never replaces the first keepAtLeast, so the elite survives immigration.
        public static List<Individual> ReplaceWeakest(IReadOnlyList<Individual> ranked, IReadOnlyList<Network> newcomers, int keepAtLeast)
        {
            int keep = Math.Max(keepAtLeast, ranked.Count - newcomers.Count);
            return ranked.Take(keep).Concat(newcomers.Take(ranked.Count - keep).Select(network => new Individual(network))).ToList();
        }

        // make returns null when it cannot make a newcomer from a host; hosts are reused in turn until enough are made.
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
