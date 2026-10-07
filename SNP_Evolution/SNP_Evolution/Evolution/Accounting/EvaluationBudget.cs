using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace SnpEvolution.Evolution.Accounting
{
    // What an evaluation was: a network scored on a task's cases, a network checked on the exhaustive engine against a
    // contract, one bound of a bounded proof, a register program run by the interpreter, or a network run under timing
    // jitter to measure its robustness.
    public enum EvaluationKind
    {
        Network,
        ExhaustiveCheck,
        ProofStep,
        InterpreterRun,
        JitterRun,
    }

    // Which part of a run a network evaluation was spent on.
    public enum EvaluationSource
    {
        Main,
        SideRun,
        Incubation,
        Verification,
        Proposals,
    }

    // Every evaluation a run makes is charged here by kind. The limit counts network evaluations only, as every
    // benchmark has so far, and the other kinds are reported beside them. A phase is a budget of its own inside this
    // one, with its own limit, whose charges also land here. Parts evolved before the run are paid for once and
    // shared by every run, so UpFront is kept out of every count.
    public sealed class EvaluationBudget
    {
        private readonly long[] kinds = new long[Enum.GetValues<EvaluationKind>().Length];
        private readonly long[] sources = new long[Enum.GetValues<EvaluationSource>().Length];
        private readonly EvaluationBudget? parent;
        private readonly EvaluationSource? source;
        private long upFront;

        // Without a limit nothing stops a search but itself.
        public EvaluationBudget(long? limit = null)
        {
            Limit = limit;
        }

        private EvaluationBudget(EvaluationBudget parent, long? limit, EvaluationSource? source)
            : this(limit)
        {
            this.parent = parent;
            this.source = source;
        }

        public long? Limit { get; }

        public long this[EvaluationKind kind] => Interlocked.Read(ref kinds[(int)kind]);

        // Network evaluations spent on one part of the run.
        public long this[EvaluationSource source] => Interlocked.Read(ref sources[(int)source]);

        public long Networks => this[EvaluationKind.Network];

        public long UpFront => Interlocked.Read(ref upFront);

        public bool IsSpent => Networks >= Limit;

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

        public BudgetReport Report() => new BudgetReport(
            Enum.GetValues<EvaluationKind>().ToDictionary(kind => kind, kind => this[kind]),
            Enum.GetValues<EvaluationSource>().ToDictionary(source => source, source => this[source]),
            UpFront);
    }

    // What a budget had spent when it was read.
    public sealed record BudgetReport(IReadOnlyDictionary<EvaluationKind, long> Kinds, IReadOnlyDictionary<EvaluationSource, long> Sources, long UpFront)
    {
        public long this[EvaluationKind kind] => Kinds[kind];

        public long Networks => this[EvaluationKind.Network];

        // Network evaluations and exhaustive checks, which is what a part's record has always counted.
        public long NetworksAndChecks => Networks + this[EvaluationKind.ExhaustiveCheck];

        public string Describe()
        {
            IEnumerable<string> spent = Enum.GetValues<EvaluationSource>().Select(source => $"{Number(Sources[source])} {Name(source)}");
            string others = string.Concat(Enum.GetValues<EvaluationKind>().Where(kind => kind != EvaluationKind.Network && Kinds[kind] > 0)
                .Select(kind => $" Besides them, {Number(Kinds[kind])} {Name(kind)}."));
            string parts = UpFront > 0 ? $" Evolving the library's parts beforehand took {Number(UpFront)}, so {Number(Networks + UpFront)} with them." : "";
            return $"Evaluations: {string.Join(", ", spent)}; {Number(Networks)} in all.{parts}{others}";
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
