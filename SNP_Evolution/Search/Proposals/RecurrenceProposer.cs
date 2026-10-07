using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Compilation;
using SnpEvolution.Specs.Contracts;

namespace SnpEvolution.Search.Proposals
{
    public sealed record ShapeProposal(string Form, IReadOnlyList<Contract> Contracts)
    {
        public string Parts => string.Join(", ", Contracts.GroupBy(contract => contract.Name).Select(group => group.Count() == 1 ? group.Key : $"{group.Count()} x {group.Key}"));
    }

    // Only an exact fit proposes anything, since a wrong part costs a whole part evolution.
    public static class RecurrenceProposer
    {
        private const int MaxDifferenceOrder = 2;

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
                return new ShapeProposal($"every {(order == 1 ? "" : "second ")}difference of the gaps is {step}", contracts);
            }
            return null;
        }
    }
}
