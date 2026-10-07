using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Evolution.Tasks;

namespace SnpEvolution.Cli
{
    // One change the advisor recommends, with why, and how to make it.
    internal sealed record Suggestion(string Setting, string Current, string Suggested, string Reason, Action<Settings> Apply);

    // What the advisor found: changes it recommends and notes that need no change, such as what a run will cost.
    internal sealed record Advice(IReadOnlyList<Suggestion> Suggestions, IReadOnlyList<string> Notes);

    // Looks over the settings for the selected task before a run and suggests changes that are known to help: a
    // likely typo in the target, a population too small to search with, iterative evolution for long targets,
    // stagnation recovery, genome limits that can express the target, and a generation budget that fits the stages.
    // A quick pilot can also race the algorithms on an early stage of the target and pick the best one.
    internal static class RunAdvisor
    {
        public const int MinimumPopulation = 30;
        public const int SuggestedPopulation = 50;
        public const int GenerationsPerStage = 150;
        public const int MaxUsefulNeurons = 30;
        public const int MaxUsefulRepetitions = 20;

        public static Advice Advise(Settings settings)
        {
            var suggestions = new List<Suggestion>();
            var notes = new List<string>();
            BenchmarkTask task = settings.SelectedTask;
            bool targetTask = settings.Task == Catalog.TargetTask;
            bool spikeTrain = task.Task.Cases.Any(@case => @case.Readout == Simulation.Readout.SpikeTrain);

            if (targetTask && settings.Target.Kind == TargetKind.Sequence && TargetPatterns.Find(settings.Target.Values) is SuspectedTypo typo)
            {
                List<int> corrected = settings.Target.Values.Select((value, index) => index == typo.Index ? typo.Expected : value).ToList();
                var fixedTarget = new OutputTarget(TargetKind.Sequence, corrected);
                suggestions.Add(new Suggestion("Target", $"{typo.Found} at position {typo.Index + 1}", typo.Expected.ToString(),
                    $"Every other value follows a pattern ({typo.Pattern}), so {typo.Found} looks like a typo; as it is, a network that follows the pattern can never score 1.",
                    target => target.Target = fixedTarget));
            }

            if (settings.Algorithm.EvolvesRulesOnly)
            {
                suggestions.Add(new Suggestion("Genetic algorithm", settings.Algorithm.Name, Catalog.StructuralDefault.Name,
                    "This algorithm only changes rule expressions, so it can never grow the structure a new system needs.",
                    target => target.Algorithm = Catalog.StructuralDefault));
            }
            else if (task.Task is IPrefixTask && settings.Algorithm != Catalog.StructuralDefault)
            {
                suggestions.Add(new Suggestion("Genetic algorithm", settings.Algorithm.Name, Catalog.StructuralDefault.Name,
                    "MAP-Elites keeps the best network for each behaviour (how much of the target it gets right, how long a gap it makes), so stepping stones survive.",
                    target => target.Algorithm = Catalog.StructuralDefault));
            }

            if (settings.PopulationSize < MinimumPopulation)
            {
                suggestions.Add(new Suggestion("Population size", settings.PopulationSize.ToString(), SuggestedPopulation.ToString(),
                    "A handful of networks per generation explores very little; most of the search goes on in the population.",
                    target => target.PopulationSize = SuggestedPopulation));
            }

            int stages = 1;
            if (task.Task is IPrefixTask prefixTask)
            {
                IReadOnlyList<int> lengths = settings.CurriculumFor(prefixTask).Lengths(prefixTask.Length);
                if (!settings.IterativeEvolution && lengths.Count > 1)
                {
                    suggestions.Add(new Suggestion("Iterative evolution", "off", "on",
                        $"Learning the {prefixTask.Length} values {lengths[0]} at first, then a few more at a time, gives each stage a small step to find instead of the whole system.",
                        target => target.IterativeEvolution = true));
                }
                stages = lengths.Count;
            }

            int suggestedGenerations = Math.Max(settings.MaxGenerations, stages * GenerationsPerStage);
            if (stages > 1 && settings.MaxGenerations < stages * GenerationsPerStage / 2)
            {
                suggestions.Add(new Suggestion("Max generations", settings.MaxGenerations.ToString(), suggestedGenerations.ToString(),
                    $"{stages} stages share the generations, and a stage can easily need {GenerationsPerStage}.",
                    target => target.MaxGenerations = suggestedGenerations));
            }

            int patience = Math.Clamp(suggestedGenerations / (stages * 4), 20, 100);
            if (!settings.StagnationRecovery)
            {
                suggestions.Add(new Suggestion("Stagnation recovery", "off", $"on, after {patience} generations",
                    "When the best fitness stops improving, raising mutation and bringing in newcomers gets the search out of a dead end.",
                    target => (target.StagnationRecovery, target.StagnationPatience) = (true, patience)));
            }
            else if (settings.StagnationPatience > 2 * patience)
            {
                suggestions.Add(new Suggestion("Stagnation patience", settings.StagnationPatience.ToString(), patience.ToString(),
                    "With this many generations per stage, waiting so long before reacting to a stall wastes most of them.",
                    target => target.StagnationPatience = patience));
            }

            AdviseGenome(settings, task, suggestions);

            if (spikeTrain && settings.Repetitions > MaxUsefulRepetitions)
            {
                suggestions.Add(new Suggestion("Runs per network", settings.Repetitions.ToString(), MaxUsefulRepetitions.ToString(),
                    "Only a network that behaves the same on every run can match a spike train, so a few runs show that; a solution is still retested before the run stops.",
                    target => target.Repetitions = MaxUsefulRepetitions));
            }

            notes.Add(CostNote(settings, task, suggestions));
            return new Advice(suggestions, notes);
        }

        public static void ApplyAll(Settings settings, Advice advice)
        {
            foreach (Suggestion suggestion in advice.Suggestions)
            {
                suggestion.Apply(settings);
            }
        }

        public static IReadOnlyList<string> Format(Advice advice)
        {
            var lines = new List<string>();
            if (advice.Suggestions.Count == 0)
            {
                lines.Add("The settings look fine for this task.");
            }
            foreach (Suggestion suggestion in advice.Suggestions)
            {
                lines.Add($"{suggestion.Setting}: {suggestion.Current} -> {suggestion.Suggested}");
                lines.Add($"    {suggestion.Reason}");
            }
            lines.AddRange(advice.Notes);
            return lines;
        }

        // Races the structural algorithms on an early stage of the task by successive halving, with the current
        // settings, and returns the winner. Early stages are short, so this is quick next to the run itself.
        public static EvolutionSearch Pilot(Settings settings, long initialBudget, Action<string> log)
        {
            BenchmarkTask task = settings.SelectedTask;
            if (task.Task is IPrefixTask prefixTask)
            {
                CurriculumPlan plan = settings.CurriculumFor(prefixTask);
                int length = Math.Min(prefixTask.Length, plan.StartLength + 2 * plan.Step);
                task = task with { Task = length >= prefixTask.Length ? prefixTask : prefixTask.Prefix(length) };
            }
            BenchmarkSettings benchmark = settings.BenchmarkSettings with { Seeds = 2, PopulationSize = settings.PopulationSize };
            List<EvolutionSearch> candidates = Catalog.Algorithms.Where(search => !search.EvolvesRulesOnly).ToList();
            log($"Pilot: racing {candidates.Count} algorithms on {task.Name}" + (task.Task is IPrefixTask stage && task.Task != settings.SelectedTask.Task ? $", first {stage.Length} values" : "") + ".");
            return AlgorithmSelector.Select(candidates, task, benchmark, initialBudget, log).Winner;
        }

        // Long gaps and fast-growing targets need room to count: delays, more spikes to start with, and rules that
        // send more than one spike at a time.
        private static void AdviseGenome(Settings settings, BenchmarkTask task, List<Suggestion> suggestions)
        {
            if (settings.MaxNeurons > MaxUsefulNeurons)
            {
                suggestions.Add(new Suggestion("Max neurons", settings.MaxNeurons.ToString(), MaxUsefulNeurons.ToString(),
                    "Known small SN P systems use far fewer neurons; a higher cap mostly makes the space to search bigger.",
                    target => target.MaxNeurons = MaxUsefulNeurons));
            }
            if (task.Task is not ISequenceTask sequence)
            {
                return;
            }
            int largest = sequence.Expected.Max();
            bool grows = sequence.Expected.Count >= 4 && sequence.Expected[^1] >= 2 * sequence.Expected[sequence.Expected.Count / 2];
            if (grows && settings.MaxNeurons < 12)
            {
                suggestions.Add(new Suggestion("Max neurons", settings.MaxNeurons.ToString(), "12",
                    "Gaps that keep growing need a counter and something to grow it, which takes more neurons than a fixed rhythm.",
                    target => target.MaxNeurons = 12));
            }
            int delay = Math.Clamp((int)Math.Ceiling(Math.Log2(largest)), 1, 3);
            if (settings.MaxDelay < delay)
            {
                suggestions.Add(new Suggestion("Max delay", settings.MaxDelay.ToString(), delay.ToString(),
                    $"Gaps up to {largest} are easier to make when a rule can wait a few steps before it fires.",
                    target => target.MaxDelay = delay));
            }
            if (grows && settings.MaxProduce < 3)
            {
                suggestions.Add(new Suggestion("Max spikes produced", settings.MaxProduce.ToString(), "3",
                    "Growing gaps need spike counts that grow, which rules sending several spikes at once make easier.",
                    target => target.MaxProduce = 3));
            }
            if (grows && !settings.DuplicateNeurons)
            {
                suggestions.Add(new Suggestion("Duplicate neurons", "off", "on",
                    "Copying a working neuron, such as one that counts, is a quick way to build the repeated parts a growing pattern needs.",
                    target => target.DuplicateNeurons = true));
            }
            if (grows && settings.MaxInitialSpikes < 8)
            {
                suggestions.Add(new Suggestion("Max initial spikes", settings.MaxInitialSpikes.ToString(), "8",
                    "More spikes to start with gives counters something to count down from.",
                    target => target.MaxInitialSpikes = 8));
            }
            // On 16 values of Fibonacci over 15 seeds, the two together solved 7.3 values on average against 6.5 for
            // a run without them given as many generations as their side runs added on average.
            if (grows && !(settings.Modules && settings.Lexicase))
            {
                suggestions.Add(new Suggestion("Modules and lexicase", $"{OnOff(settings.Modules)} and {OnOff(settings.Lexicase)}", "on and on",
                    "Growing gaps are built from parts; lexicase keeps networks that are right about different gaps, and side runs build the parts nothing makes yet.",
                    target => (target.Modules, target.Lexicase) = (true, true)));
            }
        }

        private static string OnOff(bool value) => value ? "on" : "off";

        // A rough count of the simulation steps the run could take, with the suggestions applied.
        private static string CostNote(Settings settings, BenchmarkTask task, IReadOnlyList<Suggestion> suggestions)
        {
            Settings preview = settings.Copy();
            suggestions.ToList().ForEach(suggestion => suggestion.Apply(preview));
            int steps = Math.Max(preview.MaxSteps, preview.SelectedTask.Task.StepsNeeded);
            double total = (double)steps * preview.Repetitions * preview.PopulationSize * preview.MaxGenerations;
            string iterative = EvolutionSession.IsIterative(preview, preview.SelectedTask, out _) ? ", far fewer as early stages are short" : "";
            return $"At most about {total / 1e6:0.#} million simulated steps ({steps} per run x {preview.Repetitions} runs x {preview.PopulationSize} networks x {preview.MaxGenerations} generations{iterative}).";
        }
    }
}
