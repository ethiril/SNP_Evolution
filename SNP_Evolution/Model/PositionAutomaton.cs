using System;
using System.Collections.Generic;
using System.Linq;
using static SnpEvolution.Model.ExpressionNode;

namespace SnpEvolution.Model
{
    // The Glushkov construction of a rule expression: every 'a' in it is a position, and reading one spike moves each
    // active position to those that may follow it. Each copy of a repeated body gets its own positions.
    internal sealed class PositionAutomaton
    {
        internal const int MaxPositions = 10_000;

        private readonly List<HashSet<int>> follow = new List<HashSet<int>>();
        private readonly HashSet<int> last;

        public PositionAutomaton(ExpressionNode expression)
        {
            Fragment root = Build(expression);
            Start = root.First.OrderBy(position => position).ToArray();
            last = root.Last;
        }

        // The positions active before any spike is read, in order.
        public int[] Start { get; }

        public bool Accepts(int[] active) => active.Any(last.Contains);

        // The positions active after one more spike, in order, so equal sets have equal keys.
        public int[] Next(int[] active) => active.SelectMany(position => follow[position]).Distinct().OrderBy(position => position).ToArray();

        private Fragment Build(ExpressionNode node) => node switch
        {
            Spike => NewPosition(),
            NeverMatches => new Fragment(false, new HashSet<int>(), new HashSet<int>()),
            Sequence sequence => sequence.Parts.Aggregate(Empty(), (built, part) => Concatenate(built, Build(part))),
            Choice choice => choice.Options.Select(Build).Aggregate(Union),
            Repeat repeat => BuildRepeat(repeat),
            _ => throw new InvalidOperationException("Unknown expression node."),
        };

        // Each copy of the body is built afresh, so every repetition gets its own positions.
        private Fragment BuildRepeat(Repeat repeat)
        {
            Fragment built = Empty();
            int plainCopies = repeat.Max == null && repeat.Min > 0 ? repeat.Min - 1 : repeat.Min;
            for (int copy = 0; copy < plainCopies; copy++)
            {
                built = Concatenate(built, Build(repeat.Body));
            }
            if (repeat.Max == null)
            {
                return Concatenate(built, Loop(Build(repeat.Body), repeat.Min > 0));
            }
            for (int copy = repeat.Min; copy < repeat.Max; copy++)
            {
                Fragment optional = Build(repeat.Body);
                built = Concatenate(built, optional with { Nullable = true });
            }
            return built;
        }

        private Fragment NewPosition()
        {
            if (follow.Count >= MaxPositions)
            {
                throw new ArgumentException("Rule expression repeats too many times to compile.");
            }
            follow.Add(new HashSet<int>());
            int position = follow.Count - 1;
            return new Fragment(false, new HashSet<int> { position }, new HashSet<int> { position });
        }

        private static Fragment Empty() => new Fragment(true, new HashSet<int>(), new HashSet<int>());

        private Fragment Concatenate(Fragment first, Fragment second)
        {
            foreach (int position in first.Last)
            {
                follow[position].UnionWith(second.First);
            }
            var firsts = new HashSet<int>(first.First);
            if (first.Nullable)
            {
                firsts.UnionWith(second.First);
            }
            var lasts = new HashSet<int>(second.Last);
            if (second.Nullable)
            {
                lasts.UnionWith(first.Last);
            }
            return new Fragment(first.Nullable && second.Nullable, firsts, lasts);
        }

        private static Fragment Union(Fragment first, Fragment second) =>
            new Fragment(first.Nullable || second.Nullable, new HashSet<int>(first.First.Union(second.First)), new HashSet<int>(first.Last.Union(second.Last)));

        private Fragment Loop(Fragment body, bool atLeastOnce)
        {
            foreach (int position in body.Last)
            {
                follow[position].UnionWith(body.First);
            }
            return body with { Nullable = body.Nullable || !atLeastOnce };
        }

        private sealed record Fragment(bool Nullable, HashSet<int> First, HashSet<int> Last);
    }
}
