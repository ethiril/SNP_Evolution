using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Evolution
{
    public class ModuleTests
    {
        // A fixed population that records what it is given, so the modular loop can be watched without evolving.
        private sealed class StubAlgorithm : IGeneticAlgorithm
        {
            public List<Individual> Individuals { get; } = new List<Individual>();

            public List<Network> Immigrants { get; } = new List<Network>();

            public int Rescores { get; private set; }

            public IReadOnlyList<Individual> Population => Individuals;

            public int Generation { get; private set; } = 1;

            public Individual? Best => Individuals.Count == 0 ? null : Ranking.Rank(Individuals)[0];

            public IReadOnlyList<IReadOnlyList<float>> FitnessHistory => Array.Empty<IReadOnlyList<float>>();

            public void NextGeneration() => Generation++;

            public void Immigrate(IReadOnlyList<Network> newcomers) => Immigrants.AddRange(newcomers);

            public void Rescore() => Rescores++;
        }

        private sealed class DelegateEvaluator : IPopulationEvaluator
        {
            public DelegateEvaluator(Func<Network, FitnessResult> result) => Result = result;

            public Func<Network, FitnessResult> Result { get; }

            public IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks) => networks.Select(Result).ToList();
        }

        private static Individual Scored(Network network, float fitness, params float[] checks)
        {
            var individual = new Individual(network);
            individual.Record(new FitnessResult(fitness, Array.Empty<int>(), Checks: checks));
            return individual;
        }

        // n1 -> n2 -> n3 (out), with n2 waiting a step.
        private static Network Chain() => new Network(new[]
        {
            Neuron(1, new[] { 2 }, Standard("a", 1)),
            Neuron(0, new[] { 3 }, Standard("a", 1, delay: 1)),
            OutputNeuron(0, Standard("a", 1)),
        });

        private static Module ModuleOf(ModuleLibrary library, Network network) => library.Add(ModuleCuts.Whole(network), "a test")!;

        [Fact]
        public void SequenceChecksScoreEachGapInPlaceEvenAfterAMistake()
        {
            var task = new SequenceTask("test", new[] { 2, 1, 2 });
            var evaluator = new FitnessEvaluator(new SequentialCpuEngine(), task, new SimulationOptions(20, 2, OutputTiming.Interval), 1, new Random(1));

            FitnessResult result = evaluator.Evaluate(PingPong());

            Assert.Equal(new[] { 1f, 0f, 1f }, result.Checks);
            Assert.Equal("gap 2 (1)", task.CheckName(1));
            Assert.Equal(new[] { 2, 1, 2 }, ((SequenceTask)task.Focus(1)!).Expected);
        }

        [Fact]
        public void LexicasePicksSpecialistsButNeverANetworkThatIsBestAtNothing()
        {
            var random = new Random(1);
            List<Individual> ranked = Ranking.Rank(new[]
            {
                Scored(PingPong(), 0.6f, 0.6f, 0.6f),
                Scored(Identity(), 0.5f, 1f, 0f),
                Scored(NeverOutputs(), 0.5f, 0f, 1f),
                Scored(Chain(), 0.4f, 0.5f, 0.5f),
            });
            Func<Individual> pick = new LexicaseSelection().Prepare(ranked, random);

            List<Individual> picks = Enumerable.Range(0, 200).Select(_ => pick()).ToList();

            Assert.Contains(picks, individual => individual.Fitness == 0.5f && individual.Checks[1] == 1f);
            Assert.Contains(picks, individual => individual.Fitness == 0.5f && individual.Checks[0] == 1f);
            Assert.DoesNotContain(picks, individual => individual.Fitness == 0.4f);
        }

        [Fact]
        public void DiagnosisFindsTheFirstCheckNoNetworkDoes()
        {
            CheckDiagnosis diagnosis = CheckDiagnosis.Of(new[] { Scored(PingPong(), 0.5f, 1, 1, 0, 0), Scored(Identity(), 0.4f, 1, 0, 0.5f, 1) });

            Assert.Equal(new[] { 2 }, diagnosis.Unsolved);
            Assert.Equal(2, diagnosis.Frontier);
            Assert.StartsWith("No network yet does gap 3 (5)", diagnosis.Describe(new SequenceTask("fib", new[] { 1, 2, 5, 8 })));
        }

        [Fact]
        public void CutKeepsInsideSynapsesAndFindsThePorts()
        {
            Cut cut = ModuleCuts.Cut(Chain(), new[] { 1, 2 });

            Assert.Equal(2, cut.Body.Neurons.Count);
            Assert.Equal(new[] { 2 }, cut.Body.Neurons[0].Connections);
            Assert.True(cut.Body.Neurons[1].IsOutput);
            Assert.Equal(new[] { 0 }, cut.Inputs);
            Assert.Equal(new[] { 1 }, cut.Outputs);
        }

        [Fact]
        public void PruningDropsNeuronsThatCannotReachTheOutput()
        {
            Network network = new Network(Chain().Neurons.Append(Neuron(3, new[] { 5 }, Standard("a", 1))).Append(Neuron(0, new[] { 4 }, Standard("a", 1))).ToList());

            Network pruned = ModuleCuts.Prune(network);

            Assert.Equal(3, pruned.Neurons.Count);
            Assert.Equal(Chain().Neurons.Select(neuron => neuron.Connections), pruned.Neurons.Select(neuron => neuron.Connections));
        }

        [Fact]
        public void ChangedNeuronsAreFoundWithOrWithoutRenumbering()
        {
            Network parent = Chain();
            Network nudged = parent.WithNeuron(1, parent.Neurons[1].WithInitialSpikes(2));
            Network grown = NetworkEdits.AddNeuron(parent, Neuron(1, new[] { 3 }, Standard("a", 1)));

            Assert.Equal(new[] { 1 }, ModuleCuts.Changed(parent, nudged));
            Assert.Equal(new[] { 3 }, ModuleCuts.Changed(parent, grown));
            Assert.Equal(new[] { 1, 0, 2 }, ModuleCuts.AroundChanges(nudged, new[] { 1 }, 6));
        }

        [Fact]
        public void InsertingAModuleWiresItsPortsAndTagsItsNeurons()
        {
            var library = new ModuleLibrary();
            Module module = library.Add(ModuleCuts.Cut(Chain(), new[] { 1, 2 }), "a test")!;

            for (int seed = 0; seed < 20; seed++)
            {
                Network inserted = ModuleEdits.Insert(PingPong(), module, 7, 10, new Random(seed));

                Assert.Equal(4, inserted.Neurons.Count);
                Assert.Single(inserted.Neurons, neuron => neuron.IsOutput);
                Assert.All(inserted.Neurons.Skip(2), neuron => Assert.Equal(new ModuleTag(module.Id, 7), neuron.Module));
                Assert.Contains(inserted.Neurons.Take(2), neuron => neuron.Connections.Contains(3));
                Assert.Equal(new[] { 4 }, inserted.Neurons[2].Connections);
            }
            Network full = PingPong();
            Assert.Same(full, ModuleEdits.Insert(full, module, 8, 3, new Random(1)));
        }

        [Fact]
        public void FrozenModulesRejectEditsToTheirInsidesButNotToTheirSpikes()
        {
            var library = new ModuleLibrary();
            Module module = ModuleOf(library, Chain());
            Network network = ModuleEdits.Insert(PingPong(), module, 1, 10, new Random(1));
            int last = network.Neurons.Count - 1;

            Network rewired = network.WithRule(last, 0, network.Neurons[last].Rules[0].WithDelay(2));
            Network refilled = network.WithNeuron(last, network.Neurons[last].WithInitialSpikes(5));
            Network hostChanged = network.WithRule(0, 0, network.Neurons[0].Rules[0].WithDelay(2));

            Assert.False(ModuleEdits.KeepsModules(network, rewired));
            Assert.True(ModuleEdits.KeepsModules(network, refilled));
            Assert.True(ModuleEdits.KeepsModules(network, hostChanged));
            Assert.Same(network, new ProtectModules(new DelegateMutation(_ => rewired)).Mutate(network, new Random(1)));
            Assert.All(new DissolveModule().Mutate(network, new Random(1)).Neurons, neuron => Assert.Null(neuron.Module));
        }

        private sealed class DelegateMutation : IMutation
        {
            private readonly Func<Network, Network> edit;

            public DelegateMutation(Func<Network, Network> edit) => this.edit = edit;

            public Network Mutate(Network network, Random random) => edit(network);
        }

        [Fact]
        public void TheLibraryKeepsEachPartOnceAndDropsTheLeastHelpfulWhenFull()
        {
            var library = new ModuleLibrary(capacity: 2);
            Module first = ModuleOf(library, Chain());
            Module second = ModuleOf(library, PingPong());

            Assert.Same(first, ModuleOf(library, Chain()));
            library.Credit(first.Id, improved: false);
            library.Credit(second.Id, improved: true);
            ModuleOf(library, AlwaysOutputsOne());

            Assert.Equal(2, library.Modules.Count);
            Assert.Null(library.Find(first.Id));
            Assert.Equal((1, 1), (second.Uses, second.Wins));
        }

        [Fact]
        public void TheTrackerCreditsNewModulesAndKeepsChangesThatGetANewCheckRight()
        {
            var library = new ModuleLibrary();
            Module module = ModuleOf(library, Chain());
            var tracker = new ModuleTracker(library);
            var results = new Dictionary<Network, FitnessResult>();
            IPopulationEvaluator evaluator = tracker.Watch(new DelegateEvaluator(network => results[network]));
            Network parent = PingPong();
            Network child = ModuleEdits.Insert(parent, module, library.NextInstance(), 10, new Random(1));
            Network closer = parent.WithNeuron(1, parent.Neurons[1].WithInitialSpikes(3));
            Network worse = parent.WithNeuron(0, parent.Neurons[0].WithInitialSpikes(3));
            results[parent] = new FitnessResult(0.2f, Array.Empty<int>(), Checks: new[] { 1f, 0f });
            results[closer] = new FitnessResult(0.3f, Array.Empty<int>(), Checks: new[] { 1f, 0.5f });
            results[child] = new FitnessResult(0.5f, Array.Empty<int>(), Checks: new[] { 1f, 1f });
            results[worse] = new FitnessResult(0.1f, Array.Empty<int>(), Checks: new[] { 0f, 0f });
            foreach (Network offspring in new[] { closer, child, worse })
            {
                tracker.RecordChild(parent, offspring);
            }

            evaluator.EvaluateAll(new[] { parent });
            evaluator.EvaluateAll(new[] { closer, child, worse });

            Assert.Equal((1, 1), (module.Uses, module.Wins));
            Assert.Equal(2, library.Modules.Count);
            Assert.Contains("0.200 to 0.500", library.Modules[1].Origin);
        }

        [Fact]
        public void AStallBuildsAModuleOnTheSideAndOffersItToTheBestNetworks()
        {
            var library = new ModuleLibrary();
            var task = new SequenceTask("test", new[] { 2, 2, 5, 2 });
            var main = new StubAlgorithm();
            main.Individuals.AddRange(new[] { Scored(PingPong(), 0.5f, 1, 1, 0, 0), Scored(Chain(), 0.4f, 1, 0, 0, 0) });
            var focusTasks = new List<ITask>();
            var modular = new ModularEvolution(main, library, null, () => task, (focus, _) =>
            {
                focusTasks.Add(focus);
                var side = new StubAlgorithm();
                side.Individuals.Add(Scored(AlwaysOutputsOne(), 1));
                return side;
            }, new ModulePolicy(Patience: 2, SideGenerations: 5, CompositeFraction: 0.5, IncubationGenerations: 0), 4, 10, new Random(1), _ => { });

            for (int generation = 0; generation < 3; generation++)
            {
                modular.NextGeneration();
            }

            Assert.Equal(1, modular.SideRuns);
            Assert.Equal(new[] { 2, 5, 2 }, ((SequenceTask)focusTasks.Single()).Expected);
            Assert.Single(library.Modules);
            Assert.Equal(2, main.Immigrants.Count);
            Assert.All(main.Immigrants, network => Assert.Contains(network.Neurons, neuron => neuron.Module?.Module == library.Modules[0].Id));
        }

        // The input passes the trigger on, n2 waits a step, and the output and n4 then pass a spike back and forth.
        private static Network TriggeredTwos() => new Network(new[]
        {
            InputNeuron(new[] { 2 }, Standard("a", 1)),
            Neuron(0, new[] { 3 }, Standard("a", 1)),
            new Neuron(new[] { Standard("a", 1) }, 0, new[] { 4 }, true),
            Neuron(0, new[] { 3 }, Standard("a", 1)),
        });

        [Fact]
        public void ATriggeredPartCountsItsGapsFromTheTrigger()
        {
            var task = (TriggeredSequenceTask)new SequenceTask("test", new[] { 1, 2, 2, 2, 7 }).Triggered(1)!;
            var evaluator = new FitnessEvaluator(new SequentialCpuEngine(), task, new SimulationOptions(30, 2, OutputTiming.Interval), 1, new Random(1));

            FitnessResult result = evaluator.Evaluate(TriggeredTwos());
            Cut cut = ModuleCuts.Whole(TriggeredTwos());

            Assert.Equal(new[] { 2, 2, 2 }, task.Expected);
            Assert.Equal(1, task.InputCount);
            Assert.Equal(new[] { 1f, 1f, 1f }, result.Checks);
            Assert.True(FitnessEvaluator.IsSolvingFitness(result.Fitness));
            Assert.Equal(3, cut.Body.Neurons.Count);
            Assert.Equal(new[] { 0 }, cut.Inputs);
        }

        [Fact]
        public void AModuleThatTakesTheOutputIsFedByTheOldOne()
        {
            var library = new ModuleLibrary();
            Module module = ModuleOf(library, TriggeredTwos());
            int takeovers = 0;

            for (int seed = 0; seed < 20; seed++)
            {
                Network inserted = ModuleEdits.Insert(PingPong(), module, 1, 10, new Random(seed));
                int output = inserted.Neurons.ToList().FindIndex(neuron => neuron.IsOutput);
                if (output >= PingPong().Neurons.Count)
                {
                    takeovers++;
                    int oldOutput = PingPong().Neurons.ToList().FindIndex(neuron => neuron.IsOutput);
                    Assert.Contains(output + 1, inserted.Neurons[oldOutput].Connections);
                }
            }
            Assert.InRange(takeovers, 1, 19);
        }

        [Fact]
        public void NetworksGivenAModuleIncubateAndASolvedPartIsNotEvolvedAgain()
        {
            var library = new ModuleLibrary();
            var task = new SequenceTask("test", new[] { 2, 2, 5, 2 });
            var main = new StubAlgorithm();
            main.Individuals.AddRange(new[] { Scored(PingPong(), 0.5f, 1, 1, 0, 0), Scored(Chain(), 0.4f, 1, 0, 0, 0) });
            var parts = new List<ITask>();
            var incubated = new List<IReadOnlyList<Network>>();
            var modular = new ModularEvolution(main, library, null, () => task, (sideTask, seeds) =>
            {
                var side = new StubAlgorithm();
                if (seeds == null)
                {
                    parts.Add(sideTask);
                    side.Individuals.Add(Scored(AlwaysOutputsOne(), 1));
                }
                else
                {
                    incubated.Add(seeds);
                    side.Individuals.AddRange(seeds.Select(seed => Scored(seed, 0.6f)));
                }
                return side;
            }, new ModulePolicy(Patience: 2, SideGenerations: 5, CompositeFraction: 0.5, Triggered: false, IncubationGenerations: 4), 4, 10, new Random(1), _ => { });

            for (int generation = 0; generation < 5; generation++)
            {
                modular.NextGeneration();
            }

            Module module = library.Modules.Single();
            Assert.Single(parts);
            Assert.Equal(2, incubated.Count);
            Assert.All(incubated, seeds => Assert.All(seeds, network => Assert.Contains(network.Neurons, neuron => neuron.Module?.Module == module.Id)));
            Assert.Equal(incubated.SelectMany(seeds => seeds), main.Immigrants);
            Assert.Equal((2, 2), (module.Uses, module.Wins));
        }

        [Fact]
        public void ASolvedStageIsKeptAsAModule()
        {
            var library = new ModuleLibrary();
            var main = new StubAlgorithm();
            main.Individuals.Add(Scored(PingPong(), 1));
            var modular = new ModularEvolution(main, library, null, () => new SequenceTask("twos", new[] { 2, 2 }), (_, _) => new StubAlgorithm(),
                new ModulePolicy(), 4, 10, new Random(1), _ => { });

            modular.Rescore();

            Assert.Equal(1, main.Rescores);
            Assert.Contains("solved twos", library.Modules.Single().Origin);
        }

        [Fact]
        public void ModuleTagsSurviveSavingAndAreShownInTheNotation()
        {
            var library = new ModuleLibrary();
            Network network = ModuleEdits.Insert(PingPong(), ModuleOf(library, Chain()), 3, 10, new Random(1));

            Network loaded = NetworkFiles.FromJson(NetworkFiles.ToJson(network))!;

            Assert.Equal(network.Neurons.Select(neuron => neuron.Module), loaded.Neurons.Select(neuron => neuron.Module));
            Assert.Contains("[module 1]", NetworkNotation.Format(network));
            Assert.DoesNotContain("Module", NetworkFiles.ToJson(PingPong()));
        }

        [Fact]
        public void EditsWithModulesKeepNetworksWellFormedAndModulesWhole()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var random = new Random(seed);
                var factory = new NetworkFactory(new GenomeSpace(InputCount: seed % 2, RuleForm: RuleForm.Standard, MaxNeurons: 12),
                    new ExpressionGenerator(ExpressionGenerator.ExperimentalTemplates, 4, random), random);
                var library = new ModuleLibrary();
                library.Add(ModuleCuts.Whole(Chain()), "a test");
                library.Add(ModuleCuts.Cut(Chain(), new[] { 1 }), "a test");
                WeightedMutation mutation = WeightedMutation.Structural(1, factory, modules: new ModuleSupport(library));
                Network network = factory.NewNetwork();
                for (int step = 0; step < 150; step++)
                {
                    WeightedEdit edit = mutation.Edits[random.Next(mutation.Edits.Count)];
                    Network next = edit.Edit.Mutate(network, random);
                    Assert.True(edit.Edit is DissolveModule || ModuleEdits.KeepsModules(network, next), edit.Name);
                    network = next;
                    Assert.Single(network.Neurons, neuron => neuron.IsOutput);
                    Assert.InRange(network.Neurons.Count, 1, factory.Space.MaxNeurons);
                    for (int position = 1; position <= network.Neurons.Count; position++)
                    {
                        Assert.DoesNotContain(position, network.Neurons[position - 1].Connections);
                        Assert.All(network.Neurons[position - 1].Connections, target => Assert.InRange(target, 1, network.Neurons.Count));
                    }
                }
            }
        }
    }
}
