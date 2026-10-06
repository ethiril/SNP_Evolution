---
number: 57
title: Search for register programs that compute a contract
milestone: M3
labels:
  - area: evolution
parent: m3-p1-parts-without-hand-building
---

Program search only finds programs that generate a set; a part computes a function of its inputs, so no program can be searched for one.
We want function programs: register programs whose inputs are loaded into named registers when the program starts and whose output registers are read at HALT, searched against a contract's cases with an interpreter.

Its responsibilities are:
* A program form with input registers, output registers and, for branching contracts, more than one HALT, each naming a done port
* An interpreter that runs a program on one case and reports outputs, the HALT reached and the steps taken
* A search over such programs, scored on a contract's cases

Scoring should use lexicase over the cases, with a check per output port, because lexicase is what got set programs past programs that overshoot.
The interpreter should follow every choice when the program has one, because a contract must hold on every computation.
A program should leave every register but the outputs at zero when it halts, and the search should check this, because a compiled part must end as it began.
Each count contract in `FirstParts` and `ArithmeticParts.Count` should be a valid search target without special cases, because the route has to be general.

Where: `SNP_Evolution/Compilation/RegisterMachine.cs`, `SNP_Evolution/Compilation/ProgramSearch.cs`, `SNP_Evolution/Evolution/Contracts/Contract.cs`; tests in `SNP_Evolution.Tests/Compilation/CompilerTests.cs`.

Done when:
- [ ] Hand-written function programs for add, increment, double, fan-out, register and zero test pass their contracts' cases in the interpreter
- [ ] Program search finds a program for add, fan-out and zero test on at least 8 of 10 seeds each, and the counts are in RESEARCH.md
- [ ] `dotnet test` green

Read first: RESEARCH.md "Toward general synthesis" item 2, "What was built for 7 and 9 (`compile`)"
