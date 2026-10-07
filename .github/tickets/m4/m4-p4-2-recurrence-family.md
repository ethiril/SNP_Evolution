---
number: 88
title: Show recurrence generators are general
milestone: M4
labels:
  - area: research
  - kind: benchmark
parent: m4-p4-results
---

Fibonacci has only ever been checked on its first 16 values plus four more, which shows a network extrapolates a little but not that it follows the rule.
We want the recurrence family run through both modes, with each result checked on held-out starting values and long runs, so the claim is that it follows the rule and not that it lists values.

Its responsibilities are:
* Recurrence specs: Fibonacci, g(k) = g(k-1) + 2 g(k-2), powers of two, and the family with coefficients as inputs
* Both modes per spec, on one budget
* For every verified network: the share of held-out starting values it passes, and the longest run checked

Fibonacci should be one row among the family and not the headline, because a method that only finds Fibonacci is a Fibonacci method.
The family with coefficients as inputs should be reported even if neither mode solves it, with where each stops, because that is the "solve equations" case and its limit is the finding.

Where: `RecurrenceTask` and the generator spec from M4 Part 1, `superopt` and the synthesis arm from M4 Part 2, `Compilation/RecurrenceCompiler.cs`; results in RESEARCH.md.

Done when:
- [ ] Seeds 1 to 10 run in both modes for each recurrence, with solved counts, held-out pass rates and sizes in RESEARCH.md
- [ ] Every network reported as solved passes all held-out starting values and runs twice as long, in gaps, as its training runs
- [ ] `dotnet test` green

Read first: RESEARCH.md "Spec to verified circuit", "Composing modules into machines"
