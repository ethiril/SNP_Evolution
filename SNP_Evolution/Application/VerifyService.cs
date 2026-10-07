using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Verification;
using SnpEvolution.Storage;

namespace SnpEvolution.Application
{
    // One part's proof: the bound it holds to, and the counterexample when it fails.
    public sealed record VerifiedPart(string Contract, ProvenBound Proven, TimeSpan Elapsed, string? Counterexample);

    // Proves each part's contract for every input up to a bound, raised until the time per part runs out, and records
    // the bound in the part's file.
    public static class VerifyService
    {
        // Reports each part as it is proven, since a proof can take the whole time allowed.
        public static IReadOnlyList<VerifiedPart> Run(IReadOnlyList<PartFile> parts, ProofLimits limits, Action<VerifiedPart> proven)
        {
            var results = new List<VerifiedPart>();
            foreach ((var part, string path) in parts)
            {
                var clock = Stopwatch.StartNew();
                BoundedResult result = BoundedCheck.Prove(part.Part, limits, new EvaluationBudget());
                string? counterexample = result.Verdict is Verdict.Failed failed ? CounterexampleText.Of(part.Part, failed.Counterexample) : null;
                var verified = new VerifiedPart(part.Contract.Name, result.Proven, clock.Elapsed, counterexample);
                File.WriteAllText(path, PartLibraryFiles.ToJson(part with { Proven = result.Proven }));
                results.Add(verified);
                proven(verified);
            }
            return results;
        }
    }
}
