---
number: 121
title: Synthesise small parts exactly, with counterexamples
milestone: M5
labels:
  - area: search
parent: m5-p2-search-beyond-evolution
---

Evolution finds delays and sequencers but cannot say whether a smaller one exists, and the published circuits we compare with make no minimality claim either.
We want exact synthesis: for a spec and a target, ask the solver for a network of k neurons that meets the spec on the current cases, verify any answer, add the verifier's counterexample as a case, and repeat; lower k until the solver says none exists.

Its responsibilities are:
* The CEGIS loop: solve on the cases, verify with the spec's verifier, add the counterexample, repeat
* The size loop: start from the best known size and lower k until unsatisfiable, reporting the minimum and the bound it holds for
* Symmetry breaking (neurons in a fixed order) so the solver does not search permutations
* A result that says what minimality means here: no smaller network under this target, for these steps and inputs up to this bound

The minimality claim should name its bounds (steps, input values, target limits) every time it is reported, because "minimal" without them is false in general.
Synthesis should run under a hardware target, because profile rules keep the encoding linear and the result is what chips can run.

Where: the SMT encoding from this epic; the spec verifier from M4; the search interface from M2.5.

Done when:
- [ ] Delay 1 to 4, join and fork are synthesised under `generic-if`, with sizes no larger than evolution's and a minimality result for each
- [ ] The CEGIS loop is shown to add at least one counterexample on some spec before it solves
- [ ] `dotnet test` green, with the Z3 tests skipped when Z3 is missing

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution" (Search beyond evolution)
