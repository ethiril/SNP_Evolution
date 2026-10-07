---
number: 81
title: Superoptimise a correct network against its spec
milestone: M4
labels:
  - area: evolution
  - area: cli
parent: m4-p2-superoptimisation
---

Shrinking already makes compiled networks smaller (Fibonacci from 15 neurons to 10), but it is scored on fixed values, it is not checked past them, and it does not run under the hardware profile.
We want a `superopt --spec` command: start from one or more correct networks for a spec, edit them with the shrink edits, keep a MAP-Elites map of the cheapest correct networks by hardware cost and latency, and admit a network to the map only when the verifier passes it.

Its responsibilities are:
* Seeds: the register-machine and recurrence compilers, compiled parts, part programs lowered to networks, hand-built parts, or a saved network, picked by an option
* Edits from the shrink search, plus edits that keep the network inside the hardware profile when the profile is on
* A `--search` option naming any search registered behind the search interface, evolution by default
* A map keyed by neurons and latency, scored by `HardwareCost`, holding only verified networks
* Output: the best network per cell, its cost, the step it was verified to, and its export as Verilog and NIR when it fits the profile

Every seed should pass the verifier before the search starts, because a wrong seed would make every descendant compete against a wrong reference.
A network should enter the map only after passing the verifier's held-out step, and the bounded proof should run on each new elite once, because proofs on every candidate cost too much and an unproven elite must not be reported.
Evaluations should be charged to the shared evaluation budget in the same units as the other searches, because the synthesis arm is compared on the same budget.
Under the hardware profile, seeds that do not fit should be conformed first and verified again, because conforming can change behaviour.

Where: new `Search/Superoptimiser.cs`; `Search/ShrinkSearch.cs`, `Search/Algorithms/MapElites.cs`, `Specs/Parts/HardwareCost.cs`, `Model/HardwareProfile.cs`, `Specs/Accounting/EvaluationBudget.cs`, `Compilation/RegisterMachineCompiler.cs`, `Compilation/RecurrenceCompiler.cs`, `Cli/Commands/CommandRegistry.cs`, `Application/CompileService.cs`, `Cli/Commands/ExportCommands.cs`.

Done when:
- [ ] `superopt --spec add` and `superopt --spec multiply`, seeded from compiled networks, each return a network with fewer neurons than its seed that passes held-out cases and a bounded proof, on at least 8 of 10 seeds
- [ ] `superopt --spec fibonacci --profile` returns a profile network that follows the recurrence on held-out starting values, exported to Verilog and NIR and passing co-simulation
- [ ] A seed that fails its spec is refused with the failing case
- [ ] A search registered behind the search interface runs under `superopt --search` with no change to the superoptimiser
- [ ] `dotnet test` green

Read first: RESEARCH.md "Spec to verified circuit", "What was built for 7 and 9 (`compile`)", README "Compile, then shrink" and "Exporting to hardware"
