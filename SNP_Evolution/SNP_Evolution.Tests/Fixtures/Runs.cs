using SnpEvolution.Model;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Tests.Fixtures
{
    internal static class Runs
    {
        // Long and repeated enough for the task tests' small networks to show every behaviour.
        public static readonly SimulationOptions TaskOptions = new SimulationOptions(MaxSteps: 40, Repetitions: 20, OutputTiming.Interval);

        // One step at a time, choosing rules from a fixed seed.
        public static NetworkSimulation Simulate(Network network, InputSpikes? input = null, OutputTiming timing = OutputTiming.Interval) =>
            new NetworkSimulation(CompiledNetwork.Of(network), new Random(0), input ?? InputSpikes.None, timing);

        // Scores the network on the task as a run would: on the exhaustive engine for one step and five repetitions
        // unless told otherwise.
        public static FitnessResult Evaluate(ITask task, Network network, ISimulationEngine? engine = null, SimulationOptions? options = null, int solvedRetestCount = 1) =>
            new FitnessEvaluator(engine ?? new ExhaustiveCpuEngine(), task, options ?? new SimulationOptions(1, 5, OutputTiming.Interval), solvedRetestCount, new Random(0), new EvaluationBudget())
                .Evaluate(network);

        // Scores sequence targets on the sampling engine, as the compile and shrink runs do.
        public static FitnessEvaluator SequenceEvaluator(IReadOnlyList<int> values) =>
            new FitnessEvaluator(new SequentialCpuEngine(), new SequenceTask("target", values), new SimulationOptions(50, 2, OutputTiming.Interval), 1, new Random(1), new EvaluationBudget());

        // Every number the network outputs, over all its computations.
        public static IReadOnlyList<int> Generated(Network network, int maxSteps = 400) =>
            new ExhaustiveCpuEngine().CollectOutputs(new[] { network }, new SimulationOptions(maxSteps, 20, OutputTiming.Interval), new Random(1))[0];

        // Rewards networks for having exactly three neurons, which every algorithm should manage.
        public static float ThreeNeurons(Network network) => 1f / (1 + Math.Abs(network.Neurons.Count - 3));
    }
}
