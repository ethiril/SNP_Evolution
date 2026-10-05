using Newtonsoft.Json;

namespace SnpEvolution.Networks
{
    public sealed class Rule
    {
        public Rule([JsonProperty("RuleExpression")] string expression, int delay, bool fire)
        {
            Expression = expression;
            Delay = delay;
            Fire = fire;
            Condition = SpikeCondition.Parse(expression);
        }

        // Kept in regex form for display and storage; Condition is what the simulation matches against.
        [JsonProperty("RuleExpression")]
        public string Expression { get; }

        public int Delay { get; }

        // False makes this a forgetting rule: it consumes the spikes without emitting any.
        public bool Fire { get; }

        [JsonIgnore]
        public SpikeCondition Condition { get; }

        public bool Matches(long spikes) => Condition.Matches(spikes);

        public Rule WithExpression(string expression) => new Rule(expression, Delay, Fire);
    }
}
