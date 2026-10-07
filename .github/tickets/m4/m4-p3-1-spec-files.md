---
number: 84
title: Read specs from files
milestone: M4
labels:
  - area: cli
  - area: tasks
parent: m4-p3-usable-tool
---

A new algorithm can only be given to the tool by writing C#, which no outside user of the CLI can do.
We want a small spec file format, read into the spec type, covering functions, recurrences and relations: named inputs with an encoding and a range, outputs defined by integer expressions or a checker expression, a size for training and held-out cases, and a latency bound.

Its responsibilities are:
* The format, for example `in a, b: count 0..12; out sum: count = a + b; heldout 13..24; latency 2n + 4`, with recurrences as `gap(k) = gap(k-1) + gap(k-2)` and relations as `check n % d == 0 && d > 1 && d < n`
* A parser and an evaluator for integer expressions with +, -, *, /, %, comparisons, min, max and conditionals
* `--spec <file>` on every spec command, and a spec name for the built-in specs
* Error messages that name the line and the problem

Expressions should be limited to integer arithmetic, comparisons and conditionals, with no loops or calls, because a later proof needs to turn the spec into solver terms, and that is only simple for this fragment.
The file should be read into the C# spec type and nothing downstream should know it came from a file, because two paths to one spec would score differently.
Built-in specs should be writable as files that give the same cases, because that is the test that the format covers what the code does.

Where: new `Storage/SpecFiles.cs`; `Cli/Commands/CommandRegistry.cs`, `Cli/Commands/CommonOptions.cs`, `Cli/InputParsing.cs`; the spec type from M4 Part 1; tests in `SNP_Evolution.Tests/Cli/`.

Done when:
- [ ] Add, multiply, compare, the Fibonacci recurrence and the divisor relation are each written as a spec file that gives the same cases as the built-in spec
- [ ] A spec file with an error names the line and what is wrong
- [ ] `superopt --spec-file` runs on a file for max of two counts
- [ ] `dotnet test` green

Read first: RESEARCH.md "Spec to verified circuit", README "Tasks"
