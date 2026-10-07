using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace SnpEvolution.Specs.Accounting
{
    // Only Network counts towards a budget's limit; the other kinds are reported beside it.
    public enum EvaluationKind
    {
        Network,
        ExhaustiveCheck,
        ProofStep,
        InterpreterRun,
        JitterRun,
    }

    public enum EvaluationSource
    {
        Main,
        SideRun,
        Incubation,
        Verification,
        Proposals,
    }

    // The limit counts network evaluations only, as every benchmark has, and up-front part costs stay out of every count since all runs share them.
    public sealed class EvaluationBudget
    {
        private readonly long[] kinds = new long[Enum.GetValues<EvaluationKind>().Length];
        private readonly long[] sources = new long[Enum.GetValues<EvaluationSource>().Length];
        private readonly EvaluationBudget? parent;
        private readonly EvaluationSource? source;
        private readonly long? limit;
        private long upFront;
        private long repeats;
        private long exactRepeats;

        // Without a limit nothing stops a search but itself.
        public EvaluationBudget(long? limit = null)
        {
            this.limit = limit;
        }

        private EvaluationBudget(EvaluationBudget parent, long? limit, EvaluationSource? source)
            : this(limit)
        {
            this.parent = parent;
            this.source = source;
        }

        public long this[EvaluationKind kind] => Interlocked.Read(ref kinds[(int)kind]);

        public long this[EvaluationSource source] => Interlocked.Read(ref sources[(int)source]);

        public long Networks => this[EvaluationKind.Network];

        public long UpFront => Interlocked.Read(ref upFront);

        public bool IsSpent => Networks >= limit;

        // A phase given a source charges every network evaluation to it, whatever source the evaluator was given.
        public EvaluationBudget Phase(long? limit = null, EvaluationSource? source = null) => new EvaluationBudget(this, limit, source ?? this.source);

        public void Charge(EvaluationKind kind, long count, EvaluationSource source = EvaluationSource.Main)
        {
            EvaluationSource charged = this.source ?? source;
            Interlocked.Add(ref kinds[(int)kind], count);
            if (kind == EvaluationKind.Network)
            {
                Interlocked.Add(ref sources[(int)charged], count);
            }
            parent?.Charge(kind, count, charged);
        }

        public void AddUpFront(long evaluations) => Interlocked.Add(ref upFront, evaluations);

        // Of the networks already charged, how many repeated one scored before on the same task, and how many of those
        // repeated an exact result, which a cache could have answered without simulating. Measured, not saved.
        public void CountRepeats(long count, long exact)
        {
            Interlocked.Add(ref repeats, count);
            Interlocked.Add(ref exactRepeats, exact);
            parent?.CountRepeats(count, exact);
        }

        public BudgetReport Report() => new BudgetReport(
            Enum.GetValues<EvaluationKind>().ToDictionary(kind => kind, kind => this[kind]),
            Enum.GetValues<EvaluationSource>().ToDictionary(source => source, source => this[source]),
            UpFront)
        {
            Repeats = Interlocked.Read(ref repeats),
            ExactRepeats = Interlocked.Read(ref exactRepeats),
        };
    }

    public sealed record BudgetReport(IReadOnlyDictionary<EvaluationKind, long> Kinds, IReadOnlyDictionary<EvaluationSource, long> Sources, long UpFront)
    {
        public long this[EvaluationKind kind] => Kinds[kind];

        public long Networks => this[EvaluationKind.Network];

        // Network evaluations and exhaustive checks, which is what a part's record has always counted.
        public long NetworksAndChecks => Networks + this[EvaluationKind.ExhaustiveCheck];

        // See EvaluationBudget.CountRepeats.
        public long Repeats { get; init; }

        public long ExactRepeats { get; init; }

        public string Describe()
        {
            IEnumerable<string> spent = Enum.GetValues<EvaluationSource>().Select(source => $"{Number(Sources[source])} {Name(source)}");
            string others = string.Concat(Enum.GetValues<EvaluationKind>().Where(kind => kind != EvaluationKind.Network && Kinds[kind] > 0)
                .Select(kind => $" Besides them, {Number(Kinds[kind])} {Name(kind)}."));
            string parts = UpFront > 0 ? $" Evolving the library's parts beforehand took {Number(UpFront)}, so {Number(Networks + UpFront)} with them." : "";
            string repeated = Repeats > 0 ? $" {Number(Repeats)} repeated a network already scored on the same task, {Number(ExactRepeats)} of them exactly." : "";
            return $"Evaluations: {string.Join(", ", spent)}; {Number(Networks)} in all.{parts}{repeated}{others}";
        }

        private static string Name(EvaluationSource source) => source switch
        {
            EvaluationSource.Main => "main run",
            EvaluationSource.SideRun => "side runs",
            EvaluationSource.Incubation => "incubation",
            EvaluationSource.Proposals => "proposed parts",
            _ => "verification",
        };

        private static string Name(EvaluationKind kind) => kind switch
        {
            EvaluationKind.ExhaustiveCheck => "exhaustive checks",
            EvaluationKind.ProofStep => "proof bounds",
            EvaluationKind.InterpreterRun => "program runs",
            EvaluationKind.JitterRun => "runs under jitter",
            _ => "network evaluations",
        };

        private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
