---
number: 67
title: Feed counterexamples back into the search
milestone: M3
labels:
  - area: evolution
  - area: modules
parent: m3-p3-proofs-that-scale
---

Admission checks up to twice the largest case, and a part that fails there is refused and thrown away; the add loop failed at a = 14 with cases only up to a = 6, and nothing in a search learns from such a failure.
We want counterexample-guided search: when a solving network fails `BoundedCheck`, its counterexample is added to the cases the search scores on, and the search goes on.

Its responsibilities are:
* Extra cases per run, kept apart from the contract's own cases
* A loop in `evolve-parts`, proposals, promotion and part-program search: solve, check, add the counterexample, continue
* Recording the extra cases in the part file

Extra cases should be kept beside the contract, not written into it, because loading refuses a file whose contract differs from the catalogue's.
The loop should stop after a set number of counterexamples (8 by default) and report it, because a part that keeps failing at larger values is not converging.
The counterexample should be the structured one the M2.5 verifier returns, because a counterexample as text cannot be scored.
A counterexample should be added as a full case from the specification, so lexicase sees it as one more case, because a special penalty would be one more scoring rule to tune.

Where: `SNP_Evolution/Evolution/Contracts/BoundedCheck.cs` (`Admit`), `SNP_Evolution/Evolution/Contracts/PartEvolution.cs`, `SNP_Evolution/Evolution/Tasks/ContractTask.cs`, `SNP_Evolution/Evolution/Proposals/PartProposals.cs`, `SNP_Evolution/Evolution/Modules/Promotion.cs`, `SNP_Evolution/Storage/PartLibraryFiles.cs`.

Done when:
- [ ] Given the add loop as it was before its done waited for the accumulator, composition search with the counterexample loop on finds a version that passes admission past a = 14
- [ ] A part file with extra cases loads and verifies them
- [ ] The loop's limit stops a run that keeps failing and says so
- [ ] `dotnet test` green

Read first: RESEARCH.md "Proving contracts" (Results, Admission), "Toward general synthesis" item 4
