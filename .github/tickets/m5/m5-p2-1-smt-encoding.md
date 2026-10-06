---
number: 120
title: Encode a network's bounded run in SMT
milestone: M5
labels:
  - area: search
  - area: export
parent: m5-p2-search-beyond-evolution
---

Exact synthesis, equivalence checks and inductive proofs all need a network's behaviour as solver terms, and the only symbolic form today is the Uppaal model, which a solver cannot search over.
We want an encoding of one network step, and of a run of T steps from given inputs, as SMT-LIB over integers, with the network either fixed or left as unknowns (thresholds, weights, delays, synapses) for synthesis.

Its responsibilities are:
* One step: spikes held, the rule applied, delays, delivery, port inputs and readouts, for profile rules and for regex rules whose conditions are eventually periodic
* A run of T steps for one case, and the contract's checks on it as assertions
* Unknowns: a network shape with free thresholds, weights, delays and a synapse matrix, under a target's limits
* Calling Z3 through the external tool runner, with a time limit

The step should be generated from the same semantics the engines use, not written again by hand, because a second copy of the step rule is the drift M2.5 removes.
Encoded runs of fixed networks should be checked against the engine on every step, because the encoding is only trusted once it agrees.
Rules outside the encodable fragment should be refused by name, because a silently wrong encoding would make every proof built on it wrong.

Where: new `SNP_Evolution/Export/SmtExporter.cs` or the search folder from M2.5; `SNP_Evolution/Networks/SpikeCondition.cs` (lasso tables), `SNP_Evolution/Export/ExternalTool.cs`, the step semantics from M2.5. If the inductive-proof investigation in M4 built an encoding, start from it.

Done when:
- [ ] For the first parts and 25 random profile networks, Z3 finds the encoded run equal to the engine's trace on every step
- [ ] A network with an unencodable rule is refused, naming the rule
- [ ] `dotnet test` green, with the Z3 tests skipped when Z3 is missing

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution" (Search beyond evolution, Proofs for every input), "Proving contracts"
