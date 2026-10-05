using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace SnpEvolution.Networks
{
    public sealed class Rule
    {
        private readonly Regex anchoredPattern;

        public Rule([JsonProperty("RuleExpression")] string expression, int delay, bool fire)
        {
            Expression = expression;
            Delay = delay;
            Fire = fire;
            anchoredPattern = new Regex("^" + expression + "$");
        }

        [JsonProperty("RuleExpression")]
        public string Expression { get; }

        public int Delay { get; }

        // False makes this a forgetting rule: it consumes the spikes without emitting any.
        public bool Fire { get; }

        public bool Matches(string spikes) => anchoredPattern.IsMatch(spikes);

        public Rule WithExpression(string expression) => new Rule(expression, Delay, Fire);
    }
}
