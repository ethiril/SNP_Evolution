---
number: 76
title: Score generators on a recurrence, not a sequence
milestone: M4
labels:
  - area: tasks
parent: m4-p1-specs-as-targets
---

`SequenceTask` scores one fixed run of gaps, so a network that lists the 16 Fibonacci values is as fit as one that adds, and nothing in selection rewards the rule.
We want a generator kind of spec and a `RecurrenceTask`: the network is started with its starting values on count in-ports, and its output gaps are judged against a recurrence such as g(k) = g(k-1) + g(k-2), across many starting values.

Its responsibilities are:
* A generator spec: the number of starting values, the rule as a reference function of the previous values, and an optional set of coefficients given as inputs
* Cases from varied starting values (Fibonacci 1,1; Lucas 2,1; others), with held-out cases from larger starting values and longer runs
* Scoring each gap against the rule applied to the network's own previous gaps, with the first gaps pinned to the inputs
* Lexicase checks per case and per gap position, as `SequenceTask` gives per gap

The first gaps should be pinned to the given starting values, because a rule judged only on the network's own gaps is met by plain Fibonacci from any start, and listing would win again.
A gap after a mistake should still earn credit when it follows the rule from the gaps before it, because that is the slope towards a network that adds, which a prefix score cuts off.
Coefficients as inputs should be optional, because a fixed rule is the first test and the family g(k) = c1 g(k-1) + c2 g(k-2) is the stretch.
The compiled recurrence network should be the reference that scores 1, because `RecurrenceCompiler` already makes a network that follows the rule for any start it is built with.

Where: new `SNP_Evolution/Evolution/Tasks/RecurrenceTask.cs`; `SNP_Evolution/Evolution/Tasks/SequenceTask.cs`, `SNP_Evolution/Evolution/Tasks/TriggeredSequenceTask.cs`, `SNP_Evolution/Compilation/Recurrence.cs`, `SNP_Evolution/Compilation/RecurrenceCompiler.cs`, `SNP_Evolution/Evolution/Tasks/TaskSuite.cs`; the spec type from this epic.

Done when:
- [ ] A network that lists the 16 Fibonacci gaps from 1,1 scores below 0.5 on the held-out cases
- [ ] A compiled network for the Fibonacci rule, given its starting values on ports, scores 1 on training and held-out cases
- [ ] The task is listed in `tasks` and can be picked in the menu
- [ ] `dotnet test` green

Read first: RESEARCH.md "Spec to verified circuit", "What was built for 7 and 9 (`compile`)"
