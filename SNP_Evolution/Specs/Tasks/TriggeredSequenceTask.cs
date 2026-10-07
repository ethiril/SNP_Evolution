using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Simulation;

namespace SnpEvolution.Specs.Tasks
{
    // A part that continues a sequence when told to: its one input neuron receives a spike on step 0, and the gaps
    // between its output spikes should be the expected ones, counting from step 1, the step an input neuron that
    // passes the spike straight on fires. Cut out as a module, the input neuron goes and the neurons it fed become
    // input ports, so in a host the gaps count from the step the neuron wired to those ports fires. Scored like a
    // sequence, with that step taken as the spike before the first gap.
    public sealed class TriggeredSequenceTask : ITask
    {
        public const int TriggerStep = 0;
        public const int ReferenceStep = TriggerStep + 1;

        private readonly SequenceTask sequence;

        public TriggeredSequenceTask(string name, IReadOnlyList<int> expected)
        {
            sequence = new SequenceTask(name, expected);
            Cases = new[] { new TaskCase(new InputSpikes(new[] { (IReadOnlyList<int>)new[] { TriggerStep } }), Readout.SpikeTrain) };
        }

        public string Name => sequence.Name;

        public IReadOnlyList<int> Expected => sequence.Expected;

        public int InputCount => 1;

        public IReadOnlyList<TaskCase> Cases { get; }

        public int StepsNeeded => ReferenceStep + sequence.StepsNeeded;

        public float Score(IReadOnlyList<TrialResult> results) => sequence.Score(FromReference(results));

        public string Describe(IReadOnlyList<TrialResult> results) => sequence.Describe(FromReference(results));

        public (int, int)? Niche(IReadOnlyList<TrialResult> results) => sequence.Niche(FromReference(results));

        public IReadOnlyList<float> Checks(IReadOnlyList<TrialResult> results) => sequence.Checks(FromReference(results));

        public string CheckName(int check) => sequence.CheckName(check);

        // Each spike train with the reference step put in front, ignoring any spike before it, which no host could
        // have asked for.
        private static IReadOnlyList<TrialResult> FromReference(IReadOnlyList<TrialResult> results) =>
            results.Select(result => result with
            {
                SpikeTrains = result.SpikeTrains
                    .Select(train => (IReadOnlyList<int>)train.Where(step => step > ReferenceStep).Prepend(ReferenceStep).ToList())
                    .ToList(),
            }).ToList();
    }
}
