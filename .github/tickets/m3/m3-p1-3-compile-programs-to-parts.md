---
number: 58
title: Compile function programs into verified parts
milestone: M3
labels:
  - area: modules
  - area: evolution
parent: m3-p1-parts-without-hand-building
---

A function program is not a part until it is a network with a start, a done and count ports that meets the contract on the exhaustive engine.
We want the register-machine compiler to turn a function program into a part, and `evolve-parts` to use that route when a contract has count ports.

Its responsibilities are:
* Count in-ports that fill input registers (one spike in, two stored, as the modules' registers hold 2v)
* Start firing the first instruction, and each HALT firing its done port
* Output registers drained onto the count out-ports as v spikes, finishing before done
* Verification on the exhaustive engine, admission by `BoundedCheck`, then the existing MAP-Elites shrink

The compiled part should be verified, not trusted, because the compiler's correctness by construction has only been checked for generators.
Shrinking should keep the cheapest network that still verifies, as parts from search do, because the compiled network is far larger than it needs to be.
The part file should record that it came from a compiled program and keep the program, because the paper must say which parts came from the compiler.
A `--route search|compile|both` option should choose the route (both by default for count contracts: compile, then search from the shrunk part), because the routes should be comparable.

Where: `Compilation/RegisterMachineCompiler.cs`, `Search/PartSearch.cs`, `Specs/Contracts/PortBinding.cs`, `Storage/PartLibraryFiles.cs`, `Application/PartsService.cs`.

Done when:
- [ ] The hand-written function programs for register, add, increment, fan-out and zero test compile to parts that pass their contracts on the exhaustive engine
- [ ] Each compiled part is admitted by `BoundedCheck` and shrunk, with neurons before and after shrinking reported
- [ ] A part file from the compile route loads, verifies and names its program
- [ ] `dotnet test` green

Read first: RESEARCH.md "Toward general synthesis" item 2, README "Compile, then shrink", README "Evolving library parts"
