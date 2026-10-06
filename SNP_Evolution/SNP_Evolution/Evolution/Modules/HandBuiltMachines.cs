using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
{
    // Opt-in only, since the default library is one the runs discover themselves.
    public static class HandBuiltMachines
    {
        public static ModuleLibrary Library(Action<string>? log = null)
        {
            var library = new ModuleLibrary(log: log);
            AddParts(library, log ?? (_ => { }));
            return library;
        }

        public static void AddParts(ModuleLibrary library, Action<string> log, bool addLoop = true)
        {
            foreach (Part part in HandBuiltParts.All())
            {
                library.AddPart(Verifier.Measure(part).ToLibraryPart(part, new PartOrigin(0, HandBuiltParts.Origin, 0)), HandBuiltParts.Origin);
            }
            if (!addLoop)
            {
                return;
            }
            (Composition loop, PortBinding binding) = AddLoop(library);
            Promotion.Promote(loop, ArithmeticParts.AddLoop(), binding, library, new PartOrigin(0, HandBuiltParts.Origin, 0), log);
        }

        // Each round adds b to the sum and counts n down; on the last round the gate starts shut and swallows b, so every neuron ends where it began.
        public static (Composition Composition, PortBinding Binding) AddLoop(ModuleLibrary library)
        {
            Module Find(string contract) => library.PartFor(contract) ?? throw new ArgumentException($"The library has no {contract} part.");
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
                // Joins the loop's finish with the accumulator's done, since a large a is still draining into the sum when the loop ends.
                new GlueNeuron(new[] { Rule.Standard("aa", 2) }, 0),
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
            Endpoint Glue(int neuron) => Endpoint.GlueAt(neuron);
            var links = new[]
            {
                new Link(Glue(Start), Port(Accumulator, "start")), new Link(Glue(Start), Port(Counter, "start")),
                new Link(Glue(A), Port(Accumulator, "n")), new Link(Glue(B), Port(Gate, "n")), new Link(Glue(N), Port(Counter, "n")),
                new Link(Port(Accumulator, "out"), Glue(Sum)), new Link(Port(Copy, "a"), Glue(Sum)),
                new Link(Port(Test, "nonzero"), Glue(Open)), new Link(Glue(Open), Port(Gate, "open")),
                new Link(Port(Test, "zero"), Glue(Zero)), new Link(Glue(Zero), Glue(Again)), new Link(Glue(Zero), Glue(Finish)),
                new Link(Port(Copy, "done"), Glue(Again)), new Link(Port(Copy, "done"), Glue(Finish)),
                new Link(Glue(Again), Port(Counter, "start")), new Link(Glue(Finish), Glue(Done)), new Link(Port(Accumulator, "done"), Glue(Done)),
            };
            var composition = new Composition(parts, glue, wires, links, new[] { Glue(Start), Glue(A), Glue(B), Glue(N) }, Array.Empty<Endpoint>());
            return (composition, PortLayout.AfterInputs(ArithmeticParts.AddLoop()));
        }
    }
}
