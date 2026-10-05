---
number: 29
title: Count every evaluation a run spends
milestone: M1
labels:
  - area: evolution
parent: m1-p5-composition-search
---

A comparison with flat runs is only fair if part runs, side runs and incubation are paid from the same budget as main-run generations.
We want a single evaluation counter per run that every simulation call goes through, reported in the run summary split by source (main, side run, incubation, part evolution, verification).

Part evolution done by `evolve-parts` beforehand should be reported as a separate up-front cost and added to the total when comparing, because a library built once and reused is the method's claimed advantage and the paper must show its price.

Where: `SNP_Evolution/Evolution/FitnessEvaluator.cs`, `SNP_Evolution/Evolution/Modules/ModularEvolution.cs` (side runs, incubation), `SNP_Evolution/Evolution/StagnationRecovery.cs`; summaries in `SNP_Evolution/Cli/EvolutionSession.cs`.

Done when:
- [ ] A modular run's summary shows evaluations by source and they sum to the total
- [ ] A test checks that a side run's evaluations are counted
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", section Evaluation and open questions (Budget)
