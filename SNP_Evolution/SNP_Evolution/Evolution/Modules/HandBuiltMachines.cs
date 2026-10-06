using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
{
    // Machines built by hand from the hand-built parts, which a run only uses when asked for (--hand-built on), since the
    // default is a library the run discovers itself.
    public static class HandBuiltMachines
    {
        // The hand-built parts, and the add loop promoted from them, as a library a composition run can start from.
        public static ModuleLibrary Library(Action<string>? log = null)
        {
            var library = new ModuleLibrary(log: log);
            AddParts(library, log ?? (_ => { }));
            return library;
        }

        // Adds the hand-built parts, then, unless only the leaves are wanted, promotes the add loop built from them.
        public static void AddParts(ModuleLibrary library, Action<string> log, bool addLoop = true)
        {
            foreach (Part part in HandBuiltParts.All())
            {
                library.AddPart(LibraryPart.Of(part, PartEvolution.Measure(part), new PartOrigin(0, HandBuiltParts.Origin, 0)), HandBuiltParts.Origin);
            }
            if (!addLoop)
            {
                return;
            }
            (Composition loop, PortBinding binding) = AddLoop(library);
            Promotion.Promote(loop, ArithmeticParts.AddLoop(), binding, library, new PartOrigin(0, HandBuiltParts.Origin, 0), log);
        }

        // a + n x b as a loop over registers. Each round a register drains the counter i into a fan-out, which feeds a zero
        // test and a decrement. While i is not 0 the decrement puts i - 1 back, the gate is opened and lets b through to a
        // fan-out, which sends one copy to the sum and one back into the gate for the next round. When i is 0 the gate is
        // started shut, which swallows b, so every neuron ends where it began. A glue pair after the second fan-out either
        // starts the next round or, when the zero test has left them two spikes, fires done.
        public static (Composition Composition, PortBinding Binding) AddLoop(ModuleLibrary library)
        {
            Module Find(string contract) => library.Parts.First(module => module.Part!.Contract.Name == contract);
            Module register = Find("register"), fanOut = Find("fan-out"), zeroTest = Find("zero test"), decrement = Find("decrement"), gate = Find("gate");
            const int Accumulator = 1, Counter = 2, Split = 3, Test = 4, Less = 5, Gate = 6, Copy = 7;
            Dictionary<int, Module> modules = new[] { (Accumulator, register), (Counter, register), (Split, fanOut), (Test, zeroTest), (Less, decrement), (Gate, gate), (Copy, fanOut) }
                .ToDictionary(pair => pair.Item1, pair => pair.Item2);
            List<PartInstance> parts = modules.Select(pair => new PartInstance(pair.Key, pair.Value.Id, pair.Value.Versions.Count - 1)).ToList();
            Endpoint Port(int instance, string name) => new Endpoint(instance, modules[instance].Part!.Part.Ports().Single(port => port.Port.Name == name).Position);

            const int Start = 1, A = 2, B = 3, N = 4, Sum = 5, Done = 6, Open = 7, Zero = 8, Again = 9, Finish = 10;
            Rule relay = Rule.Standard("a", 1);
            var glue = new[]
            {
                new GlueNeuron(new[] { relay }, 0),
                new GlueNeuron(new[] { relay }, 0),
                new GlueNeuron(new[] { relay }, 0),
                new GlueNeuron(new[] { relay }, 0),
                // The accumulator and the copy may both send to the sum on one step.
                new GlueNeuron(new[] { relay, Rule.Standard("aa", 2, 2) }, 0),
                new GlueNeuron(new[] { relay }, 0),
                new GlueNeuron(new[] { relay }, 0),
                new GlueNeuron(new[] { Rule.Standard("a", 1, 2) }, 0),
                // Waits on the zero test's pair, then a third spike from the copy's done tells it which of the two to do.
                new GlueNeuron(new[] { relay, Rule.Forget("aaa", 3) }, 0),
                new GlueNeuron(new[] { Rule.Forget("a", 1), Rule.Standard("aaa", 3) }, 0),
            };
            var wires = new[]
            {
                new PortWire(Counter, "out", Split, "n"), new PortWire(Counter, "done", Split, "start"),
                new PortWire(Split, "a", Test, "n"), new PortWire(Split, "b", Less, "n"), new PortWire(Split, "done", Test, "start"),
                new PortWire(Test, "nonzero", Less, "start"), new PortWire(Test, "zero", Gate, "start"),
                new PortWire(Less, "out", Counter, "n"), new PortWire(Less, "done", Gate, "start"),
                new PortWire(Gate, "out", Copy, "n"), new PortWire(Gate, "done", Copy, "start"),
                new PortWire(Copy, "b", Gate, "n"),
            };
            Endpoint G(int neuron) => Endpoint.GlueAt(neuron);
            var links = new[]
            {
                new Link(G(Start), Port(Accumulator, "start")), new Link(G(Start), Port(Counter, "start")),
                new Link(G(A), Port(Accumulator, "n")), new Link(G(B), Port(Gate, "n")), new Link(G(N), Port(Counter, "n")),
                new Link(Port(Accumulator, "out"), G(Sum)), new Link(Port(Copy, "a"), G(Sum)),
                new Link(Port(Test, "nonzero"), G(Open)), new Link(G(Open), Port(Gate, "open")),
                new Link(Port(Test, "zero"), G(Zero)), new Link(G(Zero), G(Again)), new Link(G(Zero), G(Finish)),
                new Link(Port(Copy, "done"), G(Again)), new Link(Port(Copy, "done"), G(Finish)),
                new Link(G(Again), Port(Counter, "start")), new Link(G(Finish), G(Done)),
            };
            var composition = new Composition(parts, glue, wires, links, new[] { G(Start), G(A), G(B), G(N) }, Array.Empty<Endpoint>());
            return (composition, PortBinding.AfterInputs(ArithmeticParts.AddLoop()));
        }
    }
}
