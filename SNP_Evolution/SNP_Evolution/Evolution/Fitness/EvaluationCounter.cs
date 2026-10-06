using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace SnpEvolution.Evolution.Fitness
{
    public enum EvaluationSource
    {
        Main,
        SideRun,
        Incubation,
        Verification,
        Proposals,
    }

    // Parts evolved before the run are paid for once and shared by every run, so UpFront is kept out of Total.
    public sealed class EvaluationCounter
    {
        private readonly long[] counts = new long[Enum.GetValues<EvaluationSource>().Length];
        private long upFront;

        public long this[EvaluationSource source] => Interlocked.Read(ref counts[(int)source]);

        public long Total => Enum.GetValues<EvaluationSource>().Sum(source => this[source]);

        public long UpFront => Interlocked.Read(ref upFront);

        public void Add(EvaluationSource source, long evaluations) => Interlocked.Add(ref counts[(int)source], evaluations);

        public void AddUpFront(long evaluations) => Interlocked.Add(ref upFront, evaluations);

        public string Describe()
        {
            IEnumerable<string> spent = Enum.GetValues<EvaluationSource>().Select(source => $"{Number(this[source])} {Name(source)}");
            string parts = UpFront > 0 ? $" Evolving the library's parts beforehand took {Number(UpFront)}, so {Number(Total + UpFront)} with them." : "";
            return $"Evaluations: {string.Join(", ", spent)}; {Number(Total)} in all.{parts}";
        }

        private static string Name(EvaluationSource source) => source switch
        {
            EvaluationSource.Main => "main run",
            EvaluationSource.SideRun => "side runs",
            EvaluationSource.Incubation => "incubation",
            EvaluationSource.Proposals => "proposed parts",
            _ => "verification",
        };

        private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
