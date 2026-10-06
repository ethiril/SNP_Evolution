---
number: 61
title: Add part programs and an interpreter over specifications
milestone: M3
labels:
  - area: modules
  - area: evolution
parent: m3-p2-programs-of-parts
---

A register program can only add and subtract one; a machine built from parts needs instructions that are the parts themselves.
We want a part program: a list of instructions, each calling one library part by contract on named registers (its count in-ports read registers, its count out-ports write them) and going on to the next instruction from each of its done ports, plus an interpreter that runs it using each part's `Specification`.

Its responsibilities are:
* The program form: registers, part calls with port-to-register bindings, a next instruction per done port, HALT naming a task done port
* An output instruction for sequence targets that emits a gap of a register's value, so a generator is a program too
* An interpreter that gives each case's outputs, the HALT reached and a step estimate from the parts' latencies

A call should only be allowed to a part with a specification, because the interpreter has no other way to know what it does.
A count out-port should add to its register rather than overwrite it, because two synapses into one neuron add, and the lowering must mean the same thing.
A register read by a call should be emptied by it, because a count port consumes what it reads; a program that needs a value twice must fan it out first, as the network must.
The step estimate should be marked as an estimate, because exact timing comes only from the lowered network.

Where: new `SNP_Evolution/Evolution/Programs/` (or beside `SNP_Evolution/Compilation/RegisterMachine.cs`), `SNP_Evolution/Evolution/Contracts/Specification.cs`, `SNP_Evolution/Evolution/Modules/ModuleLibrary.cs`.

Done when:
- [ ] A hand-written part program for n1 x n2 from register, add, zero test, decrement and fan-out gives every multiply case in the interpreter
- [ ] A hand-written part program for Fibonacci gaps gives the first 16 values
- [ ] A program calling a part without a specification is refused with the part's name
- [ ] `dotnet test` green

Read first: RESEARCH.md "Toward general synthesis" item 3, README "Compile, then shrink"
