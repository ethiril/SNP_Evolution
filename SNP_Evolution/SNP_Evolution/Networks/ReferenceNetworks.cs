using System.Collections.Generic;

namespace SnpEvolution.Networks
{
    public static class ReferenceNetworks
    {
        public static Network NaturalNumbers() => new Network(new List<Neuron>
        {
            new Neuron(new List<Rule> { new Rule("aa", 0, true), new Rule("a", 0, false) }, "aa", new List<int> { 2, 3, 4 }, false),
            new Neuron(new List<Rule> { new Rule("aa", 0, true), new Rule("a", 1, false) }, "aa", new List<int> { 1, 3, 4 }, false),
            new Neuron(new List<Rule> { new Rule("aa", 0, true), new Rule("aa", 1, true) }, "aa", new List<int> { 1, 2, 4 }, false),
            new Neuron(new List<Rule> { new Rule("aa", 0, true), new Rule("aaa", 0, false) }, "aa", new List<int>(), true),
        });

        public static Network EvenNumbers() => new Network(new List<Neuron>
        {
            new Neuron(new List<Rule> { new Rule("aa", 0, true), new Rule("a", 0, false) }, "aa", new List<int> { 4 }, false),
            new Neuron(new List<Rule> { new Rule("aa", 0, true), new Rule("a", 0, false) }, "aa", new List<int> { 5 }, false),
            new Neuron(new List<Rule> { new Rule("aa", 0, true), new Rule("a", 0, false) }, "aa", new List<int> { 6 }, false),
            new Neuron(new List<Rule> { new Rule("a", 1, true), new Rule("a", 0, true) }, "", new List<int> { 1, 2, 3, 7 }, false),
            new Neuron(new List<Rule> { new Rule("a", 0, true) }, "", new List<int> { 1, 2, 7 }, false),
            new Neuron(new List<Rule> { new Rule("a", 0, true) }, "", new List<int> { 3, 7 }, false),
            new Neuron(new List<Rule> { new Rule("aa", 0, true), new Rule("aaa", 0, false) }, "aa", new List<int>(), true),
        });
    }
}
