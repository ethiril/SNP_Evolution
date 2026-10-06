using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Networks
{
    // The spike counts a rule expression accepts. Expressions are regular expressions over the single letter 'a',
    // and such sets are always eventually periodic, so the set is stored as a lasso: a lookup table of TailLength
    // entries followed by a cycle of Period entries that repeats forever. Matching any count is then one lookup.
    public sealed class SpikeCondition
    {
        private const int MaxPositions = 10_000;
        private const int MaxLassoLength = 1_000_000;

        private static readonly ConcurrentDictionary<string, SpikeCondition> Compiled = new ConcurrentDictionary<string, SpikeCondition>();

        private readonly bool[] accepts;

        private SpikeCondition(bool[] accepts, int tailLength)
        {
            this.accepts = accepts;
            TailLength = tailLength;
        }

        public int TailLength { get; }

        public int Period => accepts.Length - TailLength;

        // TailLength + Period entries; a GPU backend can upload this table unchanged.
        public ReadOnlySpan<bool> Accepts => accepts;

        // Throws ArgumentException for an expression that is not valid or uses unsupported regex syntax.
        public static SpikeCondition Parse(string expression) => Compiled.GetOrAdd(expression, Compile);

        public bool Matches(long spikes) => spikes >= 0 && accepts[IndexOf(spikes)];

        // A tail and one period past from settle it, since every count after that repeats one before.
        public long? SmallestAccepted(long from = 0)
        {
            for (long count = Math.Max(0, from); count <= Math.Max(from, TailLength) + Period; count++)
            {
                if (Matches(count))
                {
                    return count;
                }
            }
            return null;
        }

        private int IndexOf(long spikes) =>
            spikes < TailLength ? (int)spikes : TailLength + (int)((spikes - TailLength) % Period);

        // Glushkov construction: every 'a' in the expression is a position, and reading n spikes moves the set of
        // active positions n times. The sets eventually repeat, which closes the lasso.
        private static SpikeCondition Compile(string expression)
        {
            var automaton = new PositionAutomaton();
            Fragment root = automaton.Build(new ExpressionParser(expression).ParseWhole());
            var accepts = new List<bool> { root.Nullable };
            var firstSeenAt = new Dictionary<string, int>();
            int[] active = root.First.OrderBy(position => position).ToArray();
            for (int count = 1; ; count++)
            {
                string key = string.Join(",", active);
                if (firstSeenAt.TryGetValue(key, out int cycleStart))
                {
                    return new SpikeCondition(accepts.ToArray(), cycleStart);
                }
                if (count > MaxLassoLength)
                {
                    throw new ArgumentException($"Rule expression '{expression}' is too complex to compile.");
                }
                firstSeenAt[key] = count;
                accepts.Add(active.Any(root.Last.Contains));
                active = active.SelectMany(automaton.Follow).Distinct().OrderBy(position => position).ToArray();
            }
        }

        private sealed record Fragment(bool Nullable, HashSet<int> First, HashSet<int> Last);

        private sealed class PositionAutomaton
        {
            private readonly List<HashSet<int>> follow = new List<HashSet<int>>();

            public IEnumerable<int> Follow(int position) => follow[position];

            public Fragment Build(Node node) => node switch
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
        }

        private abstract record Node;

        // 'a' or '.', both of which match one spike.
        private sealed record Spike : Node;

        // Any other literal character, which can never appear in a run of spikes.
        private sealed record NeverMatches : Node;

        private sealed record Sequence(IReadOnlyList<Node> Parts) : Node;

        private sealed record Choice(IReadOnlyList<Node> Options) : Node;

        private sealed record Repeat(Node Body, int Min, int? Max) : Node;

        // Recursive descent over the regex subset that can describe spike counts: literals, '.', groups,
        // alternation and the ?, *, +, {n}, {n,} and {n,m} quantifiers (optionally lazy, which matches the same counts).
        private sealed class ExpressionParser
        {
            private readonly string expression;
            private int index;

            public ExpressionParser(string expression)
            {
                this.expression = expression;
            }

            public Node ParseWhole()
            {
                Node node = ParseChoice();
                if (index < expression.Length)
                {
                    throw Invalid("unmatched ')'");
                }
                return node;
            }

            private Node ParseChoice()
            {
                var options = new List<Node> { ParseSequence() };
                while (TryConsume('|'))
                {
                    options.Add(ParseSequence());
                }
                return options.Count == 1 ? options[0] : new Choice(options);
            }

            private Node ParseSequence()
            {
                var parts = new List<Node>();
                while (index < expression.Length && expression[index] != '|' && expression[index] != ')')
                {
                    parts.Add(ParseQuantified());
                }
                return new Sequence(parts);
            }

            private Node ParseQuantified()
            {
                Node node = ParseAtom();
                if (TryParseQuantifier(out int min, out int? max))
                {
                    TryConsume('?');
                    if ((index < expression.Length && "?*+".Contains(expression[index])) || LooksLikeQuantifier())
                    {
                        throw Invalid("nested quantifier");
                    }
                    node = new Repeat(node, min, max);
                }
                return node;
            }

            private Node ParseAtom()
            {
                char next = expression[index++];
                switch (next)
                {
                    case 'a':
                    case '.':
                        return new Spike();
                    case '(':
                        if (TryConsume('?') && !TryConsume(':'))
                        {
                            throw Invalid("only '(?:' groups are supported");
                        }
                        Node inner = ParseChoice();
                        if (!TryConsume(')'))
                        {
                            throw Invalid("missing ')'");
                        }
                        return inner;
                    case '?':
                    case '*':
                    case '+':
                        throw Invalid($"quantifier '{next}' follows nothing");
                    case '{' when LooksLikeQuantifier(index - 1):
                        throw Invalid("quantifier '{' follows nothing");
                    case '[':
                    case '\\':
                    case '^':
                    case '$':
                        throw Invalid($"'{next}' is not supported in rule expressions");
                    default:
                        return new NeverMatches();
                }
            }

            private bool TryParseQuantifier(out int min, out int? max)
            {
                min = 0;
                max = null;
                if (index >= expression.Length)
                {
                    return false;
                }
                switch (expression[index])
                {
                    case '?':
                        index++;
                        max = 1;
                        return true;
                    case '*':
                        index++;
                        return true;
                    case '+':
                        index++;
                        min = 1;
                        return true;
                    case '{' when LooksLikeQuantifier():
                        int close = expression.IndexOf('}', index);
                        string[] bounds = expression.Substring(index + 1, close - index - 1).Split(',');
                        index = close + 1;
                        min = ParseBound(bounds[0]);
                        max = bounds.Length == 1 ? min : bounds[1].Length == 0 ? null : ParseBound(bounds[1]);
                        if (max < min)
                        {
                            throw Invalid("quantifier range is in reverse order");
                        }
                        return true;
                    default:
                        return false;
                }
            }

            private int ParseBound(string bound) =>
                int.TryParse(bound, out int value) && value <= MaxPositions ? value : throw Invalid("quantifier is too large");

            // Like .NET, '{' only starts a quantifier when it reads {n}, {n,} or {n,m}; otherwise it is a literal.
            private bool LooksLikeQuantifier(int at = -1)
            {
                at = at < 0 ? index : at;
                if (at >= expression.Length || expression[at] != '{')
                {
                    return false;
                }
                int close = expression.IndexOf('}', at);
                if (close < 0)
                {
                    return false;
                }
                string[] bounds = expression.Substring(at + 1, close - at - 1).Split(',');
                return bounds.Length <= 2
                    && bounds[0].Length > 0 && bounds[0].All(char.IsAsciiDigit)
                    && (bounds.Length == 1 || bounds[1].All(char.IsAsciiDigit));
            }

            private bool TryConsume(char expected)
            {
                if (index < expression.Length && expression[index] == expected)
                {
                    index++;
                    return true;
                }
                return false;
            }

            private ArgumentException Invalid(string reason) =>
                new ArgumentException($"Invalid rule expression '{expression}': {reason}.");
        }
    }
}
