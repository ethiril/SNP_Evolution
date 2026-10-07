namespace SnpEvolution.Model
{
    // What a rule's delay d does to its neuron, decided once by Rule for every engine and exporter.
    public enum DelayKind
    {
        // No delay: the rule consumes and sends on the step it applies.
        None,

        // A delayed standard rule: it consumes at once and closes the neuron for d steps, losing what is sent to it, then sends as it reopens.
        Closing,

        // A delayed legacy rule: it sends at once, then holds the neuron for d steps, still receiving, before emptying it.
        Holding,

        // It consumes at once and leaves the neuron open, and its spikes leave on step t + d, as if the axon held them.
        Axonal,
    }
}
