using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
{
    // What promoting a composition came to: a passing verdict with the module it became, or the verdict that kept it out.
    public sealed record Promoted(Verdict Verdict, Module? Module = null);

    // The target task may test less than the contract, so a solved composition is verified on the contract, and checked
    // past its cases with a bounded check, before it is kept. Each refusal is logged.
    public static class Promotion
    {
        public static Promoted PromoteSolved(Network network, ContractTask task, ModuleLibrary library, PartOrigin origin, Action<string> log)
        {
            if (Composition.Recover(network, library) is not Composition composition)
            {
                return Refused(Stop.NotAComposition, $"Not promoted: the network that solved {task.Name} is not a composition of library parts.", log);
            }
            return Promote(composition, task.Contract, task.Binding, library, origin, log);
        }

        public static Promoted Promote(Composition composition, Contract contract, PortBinding binding, ModuleLibrary library, PartOrigin origin, Action<string> log)
        {
            if (!composition.ThroughPorts(library) || PartRecipe.Of(composition, binding, library) is not PartRecipe recipe)
            {
                return Refused(Stop.NotThroughPorts, $"Not promoted to a part for {contract.Name}: something reaches inside a part other than through its ports.", log);
            }
            var part = new Part(contract, composition.Flatten(library), binding);
            PartMeasurement measurement;
            try
            {
                measurement = Verifier.Measure(part);
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
            BoundedResult admission = BoundedCheck.Admit(part, log);
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
