namespace SnpEvolution.Search.Algorithms
{
    public enum Progress
    {
        Improved,
        Waiting,

        // Patience generations in a row have gone by without improvement; the count starts again from here.
        Stalled,
    }

    // One stall rule for every search, so each only decides how to react.
    public sealed class StallDetector
    {
        private const float ImprovementTolerance = 1e-6f;

        private readonly int patience;
        private int stale;

        public StallDetector(int patience)
        {
            this.patience = patience;
        }

        public float? Best { get; private set; }

        public Progress Observe(float fitness)
        {
            if (Best == null || fitness > Best + ImprovementTolerance)
            {
                Best = fitness;
                stale = 0;
                return Progress.Improved;
            }
            if (++stale < patience)
            {
                return Progress.Waiting;
            }
            stale = 0;
            return Progress.Stalled;
        }

        // A generation that cannot be compared with the ones before, such as one cut short, counts towards a stall.
        public Progress Wait()
        {
            if (++stale < patience)
            {
                return Progress.Waiting;
            }
            stale = 0;
            return Progress.Stalled;
        }

        public void Reset()
        {
            Best = null;
            stale = 0;
        }
    }
}
