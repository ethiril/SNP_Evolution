using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Compilation;
using SnpEvolution.Evolution.Contracts;

namespace SnpEvolution.Evolution.Proposals
{
    // What the shape of a target asks for: the form fitted, in words, and the parts that form is made of, with repeats.
    public sealed record ShapeProposal(string Form, IReadOnlyList<Contract> Contracts)
    {
        public string Parts => string.Join(", ", Contracts.GroupBy(contract => contract.Name).Select(group => group.Count() == 1 ? group.Key : $"{group.Count()} x {group.Key}"));
    }

    // Reads which operations would make a sequence from its shape, for any sequence: Fibonacci is only the test. A small
    // linear recurrence (gap_k = c1 gap_(k-1) + ... + c3 gap_(k-3)) asks for one register per term, an add per sum and a
    // double or fan-out per coefficient above one. Failing that, gaps whose k-th differences are constant ask for k
    // registers and k adds, each level summing the one below. Nothing fits exactly means no proposal, since a wrong
    // part costs a whole part evolution.
    public static class RecurrenceProposer
    {
        public const int MaxDifferenceOrder = 2;

        // Null when neither form fits every gap exactly.
        public static ShapeProposal? Propose(IReadOnlyList<int> gaps)
        {
            if (Recurrence.Fit(gaps) is Recurrence recurrence)
            {
                return new ShapeProposal(recurrence.ToString(), FromRecurrence(recurrence));
            }
            return FromDifferences(gaps);
        }

        private static List<Contract> FromRecurrence(Recurrence recurrence)
        {
            List<int> terms = recurrence.Coefficients.Where(coefficient => coefficient > 0).ToList();
            var contracts = new List<Contract>();
            contracts.AddRange(terms.Select(_ => FirstParts.Named("register")));
            contracts.AddRange(Enumerable.Repeat(FirstParts.Named("add"), terms.Count - 1));
            contracts.AddRange(terms.Where(coefficient => coefficient > 1).Select(coefficient => FirstParts.Named(coefficient == 2 ? "double" : "fan-out")));
            return contracts;
        }

        // At least two values must follow from the constant difference, as Recurrence.Fit asks of a recurrence.
        private static ShapeProposal? FromDifferences(IReadOnlyList<int> gaps)
        {
            List<long> level = gaps.Select(gap => (long)gap).ToList();
            for (int order = 1; order <= MaxDifferenceOrder; order++)
            {
                level = level.Zip(level.Skip(1), (earlier, later) => later - earlier).ToList();
                if (level.Count < 3 || level.Distinct().Count() != 1 || level[0] <= 0)
                {
                    continue;
                }
                long step = level[0];
                var contracts = new List<Contract>();
                contracts.AddRange(Enumerable.Repeat(FirstParts.Named("register"), order));
                contracts.AddRange(Enumerable.Repeat(FirstParts.Named("add"), step == 1 ? order - 1 : order));
                if (step == 1)
                {
                    contracts.Add(FirstParts.Named("increment"));
                }
                string differences = order == 1 ? "difference" : $"{Ordinal(order)} difference";
                return new ShapeProposal($"every {differences} of the gaps is {step}", contracts);
            }
            return null;
        }

        private static string Ordinal(int order) => order switch
        {
            2 => "second",
            _ => throw new ArgumentOutOfRangeException(nameof(order)),
        };
    }
}
