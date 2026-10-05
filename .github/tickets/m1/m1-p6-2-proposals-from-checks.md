---
number: 33
title: Propose contracts from failing checks
milestone: M1
labels:
  - area: evolution
parent: m1-p6-promotion-proposals-arithmetic
---

When composition stalls, the run cannot ask for the part it lacks.
We want the stall handler to turn the checks nothing in the population passes into candidate contracts (for a function task, the failing examples as a sub-contract; for a sequence, the failing gap as a timer contract), evolve parts for them with `evolve-parts` machinery, and add solved ones to the library before resuming.

Proposals should go through the same contract validation and exhaustive verification as the first parts, because a part admitted on a weaker standard poisons every composition that uses it.
Each proposal and its outcome should be logged, because the paper needs to show which parts the system asked for on its own.

Where: `SNP_Evolution/Evolution/StagnationRecovery.cs`, `SNP_Evolution/Evolution/Modules/ModularEvolution.cs` (existing `Focus` and `Triggered` side runs, which this generalises), `SNP_Evolution/Evolution/Tasks/ITask.cs`.

Done when:
- [ ] A test stalls a run on purpose and shows a proposal made, solved and added
- [ ] The run log lists proposals with their outcome
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", section The loop that keeps itself going (Propose)
