using Newtonsoft.Json;

namespace SnpEvolution.Networks
{
    // Two rule forms share this class. A legacy rule (Consume is null) fires when the expression matches and empties
    // the neuron, as in the original program. A standard rule E/a^c -> a^p;d fires when the expression matches and the
    // neuron holds at least c spikes, consumes exactly c of them and sends p spikes along every synapse.
    public sealed class Rule
    {
        public Rule([JsonProperty("RuleExpression")] string expression, int delay, bool fire, long? consume = null, int? produce = null)
        {
            Expression = expression;
            Delay = delay;
            Fire = fire;
            Consume = consume;
            Produce = produce ?? 1;
            Condition = SpikeCondition.Parse(expression);
        }

        // Kept in regex form for display and storage; Condition is what the simulation matches against.
        [JsonProperty("RuleExpression")]
        public string Expression { get; }

        public int Delay { get; }

        // False makes this a forgetting rule: it consumes the spikes without emitting any.
        public bool Fire { get; }

        // The c of E/a^c; null for a legacy rule, which consumes every spike in the neuron.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public long? Consume { get; }

        // The p of a^p: spikes sent along each synapse when the rule fires. Legacy rules always send one.
        public int Produce { get; }

        [JsonIgnore]
        public bool IsStandard => Consume != null;

        [JsonIgnore]
        public SpikeCondition Condition { get; }

        // E/a^c -> a^p;d
        public static Rule Standard(string expression, long consume, int produce = 1, int delay = 0) => new Rule(expression, delay, true, consume, produce);

        // E/a^c -> λ
        public static Rule Forget(string expression, long consume) => new Rule(expression, 0, false, consume);

        public bool Matches(long spikes) => Condition.Matches(spikes);

        // Whether the rule may be applied to a neuron holding this many spikes.
        public bool Applies(long spikes) => Condition.Matches(spikes) && spikes >= (Consume ?? 0);

        public Rule WithExpression(string expression) => new Rule(expression, Delay, Fire, Consume, Produce);

        public Rule WithDelay(int delay) => new Rule(Expression, delay, Fire, Consume, Produce);

        public Rule WithFire(bool fire) => new Rule(Expression, Delay, fire, Consume, Produce);

        public Rule WithConsume(long? consume) => new Rule(Expression, Delay, Fire, consume, Produce);

        public Rule WithProduce(int produce) => new Rule(Expression, Delay, Fire, Consume, produce);
    }
}
