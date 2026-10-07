using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SnpEvolution.Model;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Search.Modules
{
    // What promoting a composition came to: a passing verdict with the module it became, or the verdict that kept it out.
    public sealed record Promoted(Verdict Verdict, Module? Module = null);

    // The target task may test less than the contract, so a solved composition is verified on the contract, and checked
    // past its cases with a bounded check, before it is kept, each charged to the budget. Each refusal is logged.
    public static class Promotion
    {
        public static Promoted PromoteSolved(Network network, ContractTask task, ModuleLibrary library, PartOrigin origin, EvaluationBudget budget, Action<string> log)
        {
            if (Composition.Recover(network, library) is not Composition composition)
            {
                return Refused(Stop.NotAComposition, $"Not promoted: the network that solved {task.Name} is not a composition of library parts.", log);
            }
            return Promote(composition, task.Contract, task.Binding, library, origin, budget, log);
        }

        public static Promoted Promote(Composition composition, Contract contract, PortBinding binding, ModuleLibrary library, PartOrigin origin, EvaluationBudget budget, Action<string> log)
        {
            if (!composition.ThroughPorts(library) || PartRecipe.Of(composition, binding, library) is not PartRecipe recipe)
            {
                return Refused(Stop.NotThroughPorts, $"Not promoted to a part for {contract.Name}: something reaches inside a part other than through its ports.", log);
            }
            var part = new Part(contract, composition.Flatten(library), binding);
            PartMeasurement measurement;
            try
            {
                measurement = Verifier.Measure(part, budget);
            }
            catch (ArgumentException exception)
            {
                return Refused(Stop.DoesNotFit, $"Not promoted to a part for {contract.Name}: {exception.Message}", log);
            }
            if (measurement.Verdict is not Verdict.Passed)
            {
                log($"Not promoted to a part for {contract.Name}: it fails the contract ({measurement.Description.Replace(Environment.NewLine, "; ")}).");
                return new Promoted(measurement.Verdict);
            }
            BoundedResult admission = BoundedCheck.Admit(part, budget, log);
            if (admission.Verdict is Verdict.Failed)
            {
                return new Promoted(admission.Verdict);
            }
            Module module = library.AddPart(measurement.ToLibraryPart(part, origin) with { Recipe = recipe, Proven = admission.Proven }, origin.Run);
            string children = string.Join(", ", recipe.Parts.Select(child => child.Contract));
            log($"Promoted the composition for {contract.Name} to module {module.Id}: {measurement.Cost}, latency {measurement.Latency}, {admission.Proven}, built from {children}.");
            return new Promoted(new Verdict.Passed(), module);
        }

        private static Promoted Refused(Stop stop, string message, Action<string> log)
        {
            log(message);
            return new Promoted(new Verdict.Unknown(new StopReason(stop, message)));
        }
    }
}
