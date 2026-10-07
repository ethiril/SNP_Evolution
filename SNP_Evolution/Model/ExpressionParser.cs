using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Model
{
    // Recursive descent over the regex subset that can describe spike counts: literals, '.', groups,
    // alternation and the ?, *, +, {n}, {n,} and {n,m} quantifiers (optionally lazy, which matches the same counts).
    internal sealed class ExpressionParser
    {
        private readonly string expression;
        private int index;

        public ExpressionParser(string expression)
        {
            this.expression = expression;
        }

        public ExpressionNode ParseWhole()
        {
            ExpressionNode node = ParseChoice();
            if (index < expression.Length)
            {
                throw Invalid("unmatched ')'");
            }
            return node;
        }

        private ExpressionNode ParseChoice()
        {
            var options = new List<ExpressionNode> { ParseSequence() };
            while (TryConsume('|'))
            {
                options.Add(ParseSequence());
            }
            return options.Count == 1 ? options[0] : new ExpressionNode.Choice(options);
        }

        private ExpressionNode ParseSequence()
        {
            var parts = new List<ExpressionNode>();
            while (index < expression.Length && expression[index] != '|' && expression[index] != ')')
            {
                parts.Add(ParseQuantified());
            }
            return new ExpressionNode.Sequence(parts);
        }

        private ExpressionNode ParseQuantified()
        {
            ExpressionNode node = ParseAtom();
            if (TryParseQuantifier(out int min, out int? max))
            {
                TryConsume('?');
                if ((index < expression.Length && "?*+".Contains(expression[index])) || LooksLikeQuantifier())
                {
                    throw Invalid("nested quantifier");
                }
                node = new ExpressionNode.Repeat(node, min, max);
            }
            return node;
        }

        private ExpressionNode ParseAtom()
        {
            char next = expression[index++];
            switch (next)
            {
                case 'a':
                case '.':
                    return new ExpressionNode.Spike();
                case '(':
                    if (TryConsume('?') && !TryConsume(':'))
                    {
                        throw Invalid("only '(?:' groups are supported");
                    }
                    ExpressionNode inner = ParseChoice();
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
                    return new ExpressionNode.NeverMatches();
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
            int.TryParse(bound, out int value) && value <= PositionAutomaton.MaxPositions ? value : throw Invalid("quantifier is too large");

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

    // A parsed rule expression.
    internal abstract record ExpressionNode
    {
        // 'a' or '.', both of which match one spike.
        internal sealed record Spike : ExpressionNode;

        // Any other literal character, which can never appear in a run of spikes.
        internal sealed record NeverMatches : ExpressionNode;

        internal sealed record Sequence(IReadOnlyList<ExpressionNode> Parts) : ExpressionNode;

        internal sealed record Choice(IReadOnlyList<ExpressionNode> Options) : ExpressionNode;

        internal sealed record Repeat(ExpressionNode Body, int Min, int? Max) : ExpressionNode;
    }
}
