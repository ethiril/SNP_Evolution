using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace SnpEvolution.Evolution
{
    // What a network evaluation was spent on: the run's own generations, a side run for a part, networks given a
    // part evolving apart, or scoring a network again to check it really solves the task.
    public enum EvaluationSource
    {
        Main,
        SideRun,
        Incubation,
        Verification,
    }

    // Every network a run scores, by what it was for, so runs that spend evaluations in different places compare on
    // one budget. Library parts evolved before the run are a cost paid once and shared by every run that uses them,
    // so they are kept apart as UpFront rather than counted in Total.
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
            _ => "verification",
        };

        private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
