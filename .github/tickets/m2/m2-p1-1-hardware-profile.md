---
number: 39
title: Add a hardware rule profile
milestone: M2
labels:
  - area: evolution
  - area: export
parent: m2-p1-hardware-profile-and-exporters
---

Integrate-and-fire hardware cannot run arbitrary regular-expression rules, so most evolved networks cannot be ported.
We want an option that restricts generation and mutation to rules of the form `a^{>=k} / a^* -> a` (fire when holding at least k, consume everything, send one spike), with forgetting rules allowed, so every network evolved under it is an integrate-and-fire network with integer threshold k and reset to zero.

Its responsibilities are:
* A setting and command-line flag that switches the profile on
* Generation and every rule mutation respecting it
* A checker that says whether any network fits the profile and, if not, which rule breaks it

Delays should be allowed and recorded as axonal delays, because NIR and Loihi support synaptic delay and SN P delays map onto them.
The profile should leave every existing default untouched, because it is opt-in.

Where: `SNP_Evolution/Evolution/ExpressionGenerator.cs`, `SNP_Evolution/Evolution/NetworkFactory.cs`, `SNP_Evolution/Evolution/Operators/`, `SNP_Evolution/Networks/Rule.cs`, `SNP_Evolution/Cli/Settings.cs`.

Done when:
- [ ] A property test shows no network made or mutated under the profile breaks it
- [ ] The checker rejects a network with `a(aa)*` and names the rule
- [ ] A short benchmark in RESEARCH.md compares solve rates with and without the profile on the first-part contracts
- [ ] `dotnet test` green

Read first: RESEARCH.md "Use cases and a practical path", item 4
