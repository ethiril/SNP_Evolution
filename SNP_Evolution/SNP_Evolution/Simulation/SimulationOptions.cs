namespace SnpEvolution.Simulation
{
    // Jitter delays what each neuron sends along each synapse by 0 to Jitter extra steps at random (see JitterBuffer);
    // only engines whose EngineSupport allows it run it.
    public sealed record SimulationOptions(int MaxSteps, int Repetitions, OutputTiming Timing = OutputTiming.Legacy, int Jitter = 0);
}
