using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Application
{
    // What the user should know before evolving for a task: limits of the target and what the settings change.
    internal static class RunNotes
    {
        public static IReadOnlyList<string> For(Settings settings, BenchmarkTask task)
        {
            var notes = new List<string>();
            if (task.Task.StepsNeeded > settings.MaxSteps)
            {
                notes.Add($"Runs last {task.Task.StepsNeeded} steps instead of {settings.MaxSteps}, so the whole target fits.");
            }
            if (task.Task.Cases.Any(@case => @case.Readout == Readout.SpikeTrain))
            {
                notes.Add("Only a network that gives this output on every run solves the task.");
            }
            if (EvolutionSession.IsIterative(settings, task, out IPrefixTask? prefixTask))
            {
                IReadOnlyList<int> lengths = settings.CurriculumFor(prefixTask).Lengths(prefixTask.Length);
                notes.Add($"Evolving iteratively in {lengths.Count} stages, from the first {lengths[0]} values to all {prefixTask.Length}.");
            }
            if (settings.Modules)
            {
                notes.Add(settings.ModuleFiles.Count > 0
                    ? $"Building from modules, starting with {settings.ModuleFiles.Count} saved network(s) and adding any the run finds."
                    : "Building from modules the run finds: changes that pay off, solved stages and side runs on what is missing.");
                if (settings.ModuleIncubation > 0)
                {
                    notes.Add($"Networks given a new module evolve apart for up to {settings.ModuleIncubation} generations before joining the run" +
                        (settings.TriggeredModules ? "; every other side run builds a part that starts on a trigger from the host." : "."));
                }
            }
            if (settings.Algorithm is CompositionSearch)
            {
                notes.Add($"Composing networks from the parts in {settings.PartLibraryFolder}{(settings.HandBuiltParts ? " and the hand-built parts" : "")}, with up to {(settings.Composition.MaxGlue > 0 ? settings.Composition.MaxGlue : settings.MaxNeurons)} glue neuron(s) and {settings.Composition.MaxParts} part copies"
                    + (settings.Modules ? "; the modular loop is left out, since harvested modules are not parts." : "."));
                if (settings.ProposeParts)
                {
                    notes.Add($"When the run stalls it proposes the parts it lacks and evolves each for up to {settings.ProposalBudget} evaluations.");
                }
                if (task.Task is IContractTask)
                {
                    notes.Add("A composition that solves the contract is promoted to a part and saved to the library.");
                }
            }
            if (settings.MaxEvaluations > 0)
            {
                notes.Add($"The run stops after {settings.MaxEvaluations} evaluations, counting side runs, incubation and retests.");
            }
            if (settings.Lexicase)
            {
                notes.Add("Parents are picked by lexicase selection, so networks right about different parts of the target all breed.");
            }
            if (settings.StagnationRecovery)
            {
                notes.Add($"After {settings.StagnationPatience} generations without improvement, mutation steps up and newcomers join.");
            }
            if (task.Task is ISequenceTask)
            {
                notes.Add("Small SN P systems produce intervals that eventually repeat, so the evolved network matches the");
                notes.Add("numbers given and need not continue the pattern after them.");
            }
            return notes;
        }

    }
}
