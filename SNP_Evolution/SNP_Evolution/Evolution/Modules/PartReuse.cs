using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
{
    // How many copies of each library part a network holds, by contract name. Direct copies are read from ModuleTag, so
    // this works for any algorithm's networks. A promoted part's own children count as nested copies, read from its
    // recipe, so a multiplication built on an add loop shows the loop directly and the loop's registers nested.
    //
    // Module.Uses and Module.Wins count something else: children scored right after a copy was put into them, and how
    // many of those beat their parent. They say whether inserting a part helped during search, not whether it is in what
    // the run ends with, which is what this counts.
    public sealed record PartCount(string Contract, int Direct, int Nested)
    {
        public int Total => Direct + Nested;
    }

    public static class PartReuse
    {
        // Parts with no copy are left out; most copies first.
        public static IReadOnlyList<PartCount> Count(Network network, ModuleLibrary library)
        {
            var direct = new Dictionary<string, int>();
            var nested = new Dictionary<string, int>();
            foreach (ModuleTag tag in ModuleEdits.Instances(network).Keys)
            {
                if (library.Find(tag.Module)?.Part is not { } part)
                {
                    continue;
                }
                direct[part.Contract.Name] = direct.GetValueOrDefault(part.Contract.Name) + 1;
                AddChildren(part.Recipe, library, nested, depth: 0);
            }
            return direct.Keys.Union(nested.Keys)
                .Select(name => new PartCount(name, direct.GetValueOrDefault(name), nested.GetValueOrDefault(name)))
                .OrderByDescending(count => count.Total).ThenBy(count => count.Contract, StringComparer.Ordinal)
                .ToList();
        }

        // Each part's copies in the best network and its mean copies per network across a population.
        public static string Describe(Network? best, IEnumerable<Network> population, ModuleLibrary library)
        {
            List<Network> networks = population.ToList();
            IReadOnlyList<PartCount> inBest = best != null ? Count(best, library) : Array.Empty<PartCount>();
            Dictionary<string, double> mean = networks.SelectMany(network => Count(network, library))
                .GroupBy(count => count.Contract)
                .ToDictionary(group => group.Key, group => (double)group.Sum(count => count.Direct) / Math.Max(1, networks.Count));
            List<string> names = inBest.Select(count => count.Contract).Union(mean.Keys.OrderByDescending(name => mean[name])).ToList();
            if (names.Count == 0)
            {
                return "No library part is in the best network or the final population.";
            }
            var lines = new List<string> { "Part                 In best (nested)   Mean per network in final population" };
            foreach (string name in names)
            {
                PartCount? count = inBest.FirstOrDefault(each => each.Contract == name);
                string kind = library.Parts.FirstOrDefault(module => module.Part!.Contract.Name == name)?.Part!.IsComposite == true ? " (promoted)" : "";
                lines.Add($"{(name + kind),-20} {count?.Direct ?? 0,7} ({count?.Nested ?? 0})   {mean.GetValueOrDefault(name):0.00}");
            }
            return string.Join(Environment.NewLine, lines) + Environment.NewLine;
        }

        // A recipe that names itself, which loading cannot build, would recurse, so depth stops it.
        private static void AddChildren(PartRecipe? recipe, ModuleLibrary library, Dictionary<string, int> nested, int depth)
        {
            if (recipe == null || depth > 16)
            {
                return;
            }
            foreach (RecipePart child in recipe.Parts)
            {
                nested[child.Contract] = nested.GetValueOrDefault(child.Contract) + 1;
                AddChildren(library.Parts.FirstOrDefault(module => module.Part!.Contract.Name == child.Contract)?.Part!.Recipe, library, nested, depth + 1);
            }
        }
    }
}
