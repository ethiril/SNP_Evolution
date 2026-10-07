---
number: 145
title: Rebuild composite parts when a part they use gets smaller
milestone: M4
labels:
  - area: modules
parent: m4-p2-superoptimisation
---

A promoted part (the add loop, or a promoted composition) is saved with its recipe, but its network is the flattened one from when it was promoted, so a smaller copy of one of its children later does not make it smaller.
We want superoptimising or replacing a part to rebuild every composite part whose recipe uses it, verify the rebuilt part on its contract, prove it to its old bound, and keep it when it is cheaper.

Its responsibilities are:
* Finding the composites whose recipes name the replaced part, nested ones included, innermost first
* Rebuilding each from its recipe with the new child, and its glue unchanged
* Verifying and proving the rebuilt part, keeping it only when it passes and is cheaper or faster
* Logging each rebuild: the part, the child that changed, the cost before and after, and the result

A rebuilt part should be verified again even though its children are, because a child with a different latency can break timing the glue relies on.
A rebuild that fails should keep the old composite and say which check failed, because the old one is still correct.
Rebuilding should be offered after the superoptimiser saves a part and on its own as `superopt --rebuild`, because a library edited by hand needs it too.

Where: `Specs/Parts/PartRecipe.cs`, `Specs/Parts/Composition.cs`, `Specs/Parts/PartWiring.cs`, `Search/Modules/Promotion.cs`, `Specs/Parts/ModuleLibrary.cs`, `Specs/Verification/Verifier.cs`, `Specs/Verification/BoundedCheck.cs`, `Application/PartLibraries.cs`.

Done when:
- [ ] Replacing the register with a smaller verified register rebuilds the add loop, which passes its contract to its old bound with fewer neurons
- [ ] A child that breaks a composite's timing leaves the old composite kept and logs the failing check
- [ ] `dotnet test` green

Read first: RESEARCH.md "Composing modules into machines", "Proving contracts"
