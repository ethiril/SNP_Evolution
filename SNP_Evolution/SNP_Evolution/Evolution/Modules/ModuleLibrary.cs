using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
{
    // A part kept for reuse: a cut, where it came from, and how often putting a copy of it into a network made a
    // child fitter than its parent. A verified part with a contract also carries it as Part; a cheaper part with the
    // same behaviour can later take its place under the same id.
    public sealed class Module
    {
        public Module(int id, Cut cut, string origin, LibraryPart? part = null)
        {
            Id = id;
            Cut = cut;
            Origin = origin;
            Part = part;
            Versions = part != null ? new[] { part } : Array.Empty<LibraryPart>();
        }

        public int Id { get; }

        public Cut Cut { get; private set; }

        public Network Body => Cut.Body;

        public string Origin { get; private set; }

        // Null for a module harvested during a run, which has no contract.
        public LibraryPart? Part { get; private set; }

        // Kept so a copy put into a network before a cheaper part replaced it is still known by its ports.
        public IReadOnlyList<LibraryPart> Versions { get; private set; }

        // Children scored with a new copy of the module in them, and how many of those beat their parent.
        public int Uses { get; private set; }

        public int Wins { get; private set; }

        // Wins over uses, starting from a half so a new module gets tried.
        public double SuccessRate => (Wins + 1.0) / (Uses + 2.0);

        internal void Credit(bool improved)
        {
            Uses++;
            Wins += improved ? 1 : 0;
        }

        internal void Replace(LibraryPart part, string origin)
        {
            Cut = ModuleLibrary.CutOf(part.Part);
            Origin = origin;
            Part = part;
            Versions = Versions.Append(part).ToList();
        }
    }

    // The parts evolution has found so far, shared by every network in a run. Modules come from the parts of a child
    // that made it beat its parent, from networks that solved a stage, and from small runs on the side; hand-built
    // ones can be added too. Insertion picks modules in proportion to how often they have helped, and when the
    // library is full the one that has helped least makes room, so parts that are never any use drop out.
    public sealed class ModuleLibrary
    {
        public const int DefaultCapacity = 24;
        public const int MaxModuleNeurons = 24;

        private readonly object gate = new object();
        private readonly List<Module> modules = new List<Module>();
        private readonly int capacity;
        private readonly Action<string> log;
        private int nextId = 1;
        private int nextInstance = 1;

        public ModuleLibrary(int capacity = DefaultCapacity, Action<string>? log = null)
        {
            this.capacity = capacity;
            this.log = log ?? (_ => { });
        }

        public IReadOnlyList<Module> Modules
        {
            get
            {
                lock (gate)
                {
                    return modules.ToList();
                }
            }
        }

        // Every module ever added, including any that were dropped to make room.
        public int Added { get; private set; }

        // Keeps the cut unless it is empty or too big, and returns its module, which is the one already kept when
        // the same part was found before.
        public Module? Add(Cut cut, string origin)
        {
            if (cut.Body.Neurons.Count == 0 || cut.Body.Neurons.Count > MaxModuleNeurons)
            {
                log($"Not kept as a module: {cut.Body.Neurons.Count} neuron(s) from {origin}, which is more than {MaxModuleNeurons}.");
                return null;
            }
            string key = cut.Key;
            lock (gate)
            {
                if (modules.FirstOrDefault(module => module.Cut.Key == key) is Module known)
                {
                    return known;
                }
                if (modules.Count(module => module.Part == null) >= capacity)
                {
                    Module weakest = modules.Where(module => module.Part == null).OrderBy(module => module.SuccessRate).ThenBy(module => module.Id).First();
                    modules.Remove(weakest);
                }
                var added = new Module(nextId++, cut, origin);
                modules.Add(added);
                Added++;
                log($"Module {added.Id} kept: {cut.Body.Neurons.Count} neuron(s), from {origin}.");
                return added;
            }
        }

        // Keeps a verified part unless one with the same behaviour is kept already, in which case the cheaper of the two
        // stays under the kept one's id. Parts are never dropped to make room and do not count against the capacity.
        public Module AddPart(LibraryPart part, string origin)
        {
            lock (gate)
            {
                if (modules.FirstOrDefault(module => module.Part?.Behaviour == part.Behaviour) is Module known)
                {
                    if (part.Cost.CompareTo(known.Part!.Cost) < 0)
                    {
                        log($"Module {known.Id} ({part.Contract.Name}) replaced: {known.Part.Cost} by {part.Cost}, from {origin}.");
                        known.Replace(part, origin);
                    }
                    return known;
                }
                var added = new Module(nextId++, CutOf(part.Part), origin, part);
                modules.Add(added);
                Added++;
                log($"Module {added.Id} kept: part for {part.Contract.Name}, {part.Cost}, from {origin}.");
                return added;
            }
        }

        // The contract parts kept, in the order they were first added.
        public IReadOnlyList<Module> Parts => Modules.Where(module => module.Part != null).ToList();

        // The whole part network, with its input neurons as the cut's inputs and its port neurons as the outputs.
        internal static Cut CutOf(Part part)
        {
            Network network = part.Network;
            var body = new Network(network.Neurons.Select(neuron => neuron.WithRoles(neuron.IsOutput, false)).ToList());
            List<int> inputs = Enumerable.Range(0, network.Neurons.Count).Where(index => network.Neurons[index].IsInput).ToList();
            List<int> outputs = part.Binding.Positions.Values.Select(position => position - 1).Where(index => index < network.Neurons.Count).OrderBy(index => index).ToList();
            return new Cut(body, inputs, outputs);
        }

        public Module? Find(int id)
        {
            lock (gate)
            {
                return modules.FirstOrDefault(module => module.Id == id);
            }
        }

        // A module picked in proportion to its success rate, or null when the library is empty.
        public Module? Choose(Random random)
        {
            lock (gate)
            {
                if (modules.Count == 0)
                {
                    return null;
                }
                double remaining = random.NextDouble() * modules.Sum(module => module.SuccessRate);
                foreach (Module module in modules)
                {
                    if (remaining < module.SuccessRate)
                    {
                        return module;
                    }
                    remaining -= module.SuccessRate;
                }
                return modules[^1];
            }
        }

        public void Credit(int id, bool improved)
        {
            lock (gate)
            {
                modules.FirstOrDefault(module => module.Id == id)?.Credit(improved);
            }
        }

        // A fresh number for a copy of a module put into a network.
        public int NextInstance()
        {
            lock (gate)
            {
                return nextInstance++;
            }
        }

        // Every module with its record, most helpful first, and the module itself.
        public string Describe()
        {
            List<Module> kept = Modules.OrderByDescending(module => module.SuccessRate).ToList();
            var lines = new List<string> { $"{Added} module(s) found, {kept.Count} kept. Uses are children scored with a new copy; wins beat their parent." };
            foreach (Module module in kept)
            {
                lines.Add($"\nModule {module.Id}: {module.Body.Neurons.Count} neuron(s), inputs {Ports(module.Cut.Inputs)}, outputs {Ports(module.Cut.Outputs)}, " +
                    $"{module.Wins}/{module.Uses} wins, from {module.Origin}");
                lines.Add(NetworkNotation.Format(module.Body).TrimEnd());
            }
            return string.Join(Environment.NewLine, lines) + Environment.NewLine;
        }

        private static string Ports(IReadOnlyList<int> ports) => ports.Count == 0 ? "none" : string.Join(", ", ports.Select(port => "n" + (port + 1)));
    }
}
