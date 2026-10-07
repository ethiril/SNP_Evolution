---
number: 124
title: Add an enumerative search that skips equivalent candidates
milestone: M5
labels:
  - area: search
parent: m5-p2-search-beyond-evolution
---

Program synthesis results are reported against bottom-up enumeration, and evolution spends evaluations on networks that read the same as ones already scored.
We want an enumerative search: networks in order of size under a target, keeping one network per behaviour on the current cases, stopping at the first that passes the verifier.

Its responsibilities are:
* Enumeration by neurons, then synapses, then rules, under a target's limits
* Pruning by behaviour: two networks with the same port traces on every case are one
* Counting evaluations in the same units as the other searches

The behaviour key should be the same one the library uses to find duplicate parts, because two definitions of "behaves the same" would disagree.
The search should report how far it got when it runs out of budget, because the size it exhausted is a lower bound on the smallest network.

Where: the search interface from M2.5; `Specs/Parts/ModuleLibrary.cs` (behaviour dedupe), `Specs/Accounting/EvaluationBudget.cs`.

Done when:
- [ ] The enumerative search solves delay 1 to 4 and reports the size it exhausted for join and bit-serial add within the default budget
- [ ] `dotnet test` green

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution" (Enumeration with observational equivalence)
