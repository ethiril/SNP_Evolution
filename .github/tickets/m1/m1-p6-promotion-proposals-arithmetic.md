---
number: 31
title: M1 Part 6: Promotion, proposals and arithmetic
milestone: M1
labels:
  - Epic
---

A solved composition is thrown away when the run ends, and a stalled run has no way to say which part it is missing, so the library never grows past what was seeded as goals.
We want solved compositions promoted to parts, new contracts proposed when a run stalls, and arithmetic tasks that test whether the library compounds.

Its responsibilities are:
* Promotion with parts referring to parts, so no size cap applies to composites
* Proposals from failing checks
* Proposals from the shape of the target (finite differences, a fitted linear recurrence)
* Arithmetic tasks: subtraction, multiplication, division, comparison
* Published hand-built circuits as benchmark targets
* Measuring reuse

Proposals should work for any recurrent target, because a Fibonacci-specific proposer is a seed by another name.
Reuse should be measured, not assumed, because Berlot-Attwell et al. (2024) found learned libraries were rarely reused.

Order: promotion; proposals from failing checks; proposals from target shape; arithmetic tasks; published-circuit benchmarks; reuse metric.

Done when: n1 x n2 is solved by a composition that reuses a promoted add-loop part, and the reuse metric is reported.

Read first: Claude Doc "Composing SN P Modules into Machines" (The loop that keeps itself going, Build plan step 6, Evaluation); RESEARCH.md "Use cases and a practical path" item 2
