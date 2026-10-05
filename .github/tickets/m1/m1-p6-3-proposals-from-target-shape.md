---
number: 34
title: Propose contracts from the shape of the target
milestone: M1
labels:
  - area: evolution
parent: m1-p6-promotion-proposals-arithmetic
---

Failing checks say what is wrong but not which operation would fix it.
We want a proposer that fits the target sequence with finite differences and a small integer linear recurrence (gap_k = sum of c_i times gap_(k-i), order up to 3), and turns the fitted form into contracts: one register per term, an add per sum, a double or fan-out per coefficient above one.

The proposer should take any sequence task, because the method has to be general and Fibonacci is only the test.
When no recurrence fits exactly, it should propose nothing rather than a best guess, because a wrong part costs a full part evolution.
An LLM proposer is out of scope here and stays an open question in the spec.

Where: new `SNP_Evolution/Evolution/Proposals/RecurrenceProposer.cs`; `SNP_Evolution/Compilation/Recurrence.cs` may already fit recurrences and should be reused if so.

Done when:
- [ ] Fibonacci gaps give two registers and an add; 2^k gives a register and a double; a random sequence gives nothing
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", section The loop that keeps itself going (Propose)
