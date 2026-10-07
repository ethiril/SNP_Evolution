using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Simulation
{
    // The spikes the environment sends to a network's input neurons: for each input, the steps (from 0) at which
    // one spike arrives. Inputs the network has no neuron for are dropped.
    public sealed class InputSpikes
    {
        public static readonly InputSpikes None = new InputSpikes(Array.Empty<IReadOnlyList<int>>());

        public InputSpikes(IReadOnlyList<IReadOnlyList<int>> stepsPerInput)
        {
            StepsPerInput = stepsPerInput.Select(steps => (IReadOnlyList<int>)steps.OrderBy(step => step).ToArray()).ToArray();
            LastStep = StepsPerInput.SelectMany(steps => steps).DefaultIfEmpty(-1).Max();
        }

        public IReadOnlyList<IReadOnlyList<int>> StepsPerInput { get; }

        // -1 when there is no input at all.
        public int LastStep { get; }

        // The usual SN P encoding of a number n, which an interval port uses too: two spikes n steps apart, from the given step.
        public static IReadOnlyList<int> Interval(int n, int from = 0) => new[] { from, from + n };

        // Each number on its own input, so input k receives 1 0^(n-1) 1.
        public static InputSpikes Numbers(params int[] numbers) => new InputSpikes(numbers.Select(number => Interval(number)).ToArray());

        // One spike on each given step, all to the first input, as a sensor would send them.
        public static InputSpikes Train(IEnumerable<int> steps) => new InputSpikes(new[] { (IReadOnlyList<int>)steps.ToArray() });

        public int SpikesArriving(int input, int step)
        {
            IReadOnlyList<int> steps = StepsPerInput[input];
            int arriving = 0;
            for (int index = 0; index < steps.Count; index++)
            {
                if (steps[index] == step)
                {
                    arriving++;
                }
            }
            return arriving;
        }

        public override string ToString() => string.Join(" ", StepsPerInput.Select(steps => "{" + string.Join(",", steps) + "}"));
    }
}
