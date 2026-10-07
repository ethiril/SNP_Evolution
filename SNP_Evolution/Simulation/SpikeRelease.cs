namespace SnpEvolution.Simulation
{
    // What a neuron's part of a step did, as output decoding reads it.
    internal enum SpikeRelease : byte
    {
        None,
        Fired,
        Forgot,
    }
}
