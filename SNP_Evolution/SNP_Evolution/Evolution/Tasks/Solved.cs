namespace SnpEvolution.Evolution.Tasks
{
    // When a score counts as solved, once for each kind of score.
    public static class Solved
    {
        // A score sampled over random runs: a computation the samples happened to miss should not cost a solve, so one
        // run in a few dozen may go wrong. Most tasks ask this much (ITask.SolvedFitness).
        public const float Sampled = 0.985f;

        // A score every run must earn: a part either meets its contract or does not, since one neuron left holding a
        // spike means it cannot be started again; a stream window is right or wrong.
        public const float EveryRun = 1f;

        // A check's best score over a population: the check counts as done when some network gets it right on almost
        // every run, which is what CheckDiagnosis reads as the frontier.
        public const float Check = 0.95f;

        // At least the sampled bar, and the task's own where it asks for more.
        public static bool Solves(float fitness, ITask? task = null) => fitness >= Sampled && fitness <= 1 && fitness >= (task?.SolvedFitness ?? 0);
    }
}
