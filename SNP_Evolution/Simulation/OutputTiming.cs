namespace SnpEvolution.Simulation
{
    // How the output neuron's spikes become a number.
    public enum OutputTiming
    {
        // The original program's count: every step that is not an output spike, from the start until the second spike.
        Legacy,

        // The SN P definition: the number of steps between the output neuron's first and second spikes.
        Interval,
    }
}
