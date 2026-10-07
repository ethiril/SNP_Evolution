---
number: 80
title: Verify spec candidates in steps, feeding counterexamples back
milestone: M4
labels:
  - area: modules
  - area: evolution
parent: m4-p2-superoptimisation
---

A candidate is checked against a spec in several ways that differ in cost by orders of magnitude (a training run, held-out runs, a bounded proof), and running all of them on every candidate would stall the search.
We want one verifier for a spec that runs the checks cheapest first, stops at the first failure, and turns a held-out failure or a bounded-proof counterexample into a case the search scores on from then on.

Its responsibilities are:
* The steps, in order: training cases, held-out cases, `BoundedCheck` to a bound set by the spec
* A result per candidate: the step it reached, the bound proven, and any counterexample
* Adding each counterexample to the spec's counterexample cases, using the counterexample loop from M3 rather than a second one
* Recording the steps passed and the cases added beside the saved network

A held-out failure should become a counterexample case like a proof's, because both are inputs the search never saw and should not see twice.
Only candidates that pass training should go on to later steps, because held-out runs and proofs cost far more and most candidates fail training.
The step reached should be part of the saved result, because a network that passed held-out cases but was never proven must not be reported as proven.

Where: new `Specs/Verification/SpecVerifier.cs`; `Specs/Verification/BoundedCheck.cs`, `Search/PartSearch.cs`, `Storage/PartLibraryFiles.cs`, `Cli/Commands/VerifyCommand.cs`.

Done when:
- [ ] The add loop as it was before its done waited for the accumulator fails at the held-out or proof step with a counterexample at a = 14 or below, and the counterexample is added as a case
- [ ] A candidate that fails training never reaches a held-out run or a proof, shown by a test that counts engine calls
- [ ] `verify --spec` prints the step each saved network reached
- [ ] `dotnet test` green

Read first: RESEARCH.md "Proving contracts" (Results, Admission), "Toward general synthesis" item 4
