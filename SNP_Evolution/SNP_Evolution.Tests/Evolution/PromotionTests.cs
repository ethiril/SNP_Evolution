using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Networks;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.Evolution.ModuleFixtures;

namespace SnpEvolution.Tests.Evolution
{
    public sealed class PromotionTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "promotion-" + Guid.NewGuid().ToString("N"));
        private readonly List<string> log = new List<string>();

        public void Dispose()
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private static readonly PartOrigin Origin = new PartOrigin(1, "a test", 0);

        // k copies of a part with ports n, out, start and done, each draining into the next, read from the last.
        private static (Composition Composition, PortBinding Binding) Chain(ModuleLibrary library, Module module, int k)
        {
            Dictionary<string, int> ports = module.Part!.Part.Ports().ToDictionary(port => port.Port.Name, port => port.Position);
            var composition = new Composition(
                Enumerable.Range(1, k).Select(instance => new PartInstance(instance, module.Id, module.Versions.Count - 1)).ToList(),
                Array.Empty<GlueNeuron>(),
                Enumerable.Range(1, k - 1).SelectMany(instance => new[] { new PortWire(instance, "out", instance + 1, "n"), new PortWire(instance, "done", instance + 1, "start") }).ToList(),
                Array.Empty<Link>(),
                new[] { new Endpoint(1, ports["start"]), new Endpoint(1, ports["n"]) },
                Array.Empty<Endpoint>());
            IReadOnlyList<Endpoint> layout = composition.Layout(library);
            int Position(string port) => layout.ToList().IndexOf(new Endpoint(k, ports[port])) + 1;
            return (composition, new PortBinding(new Dictionary<string, int> { ["out"] = Position("out"), ["done"] = Position("done") }));
        }

        private static (ModuleLibrary Library, Module Increment) Increments()
        {
            var library = new ModuleLibrary();
            return (library, library.AddPart(Verified(ReferenceParts.Increment()), "a test"));
        }

        private static string Json(Network network) => NetworkFiles.ToJson(network);

        [Fact]
        public void TheTwoIncrementChainPromotedAsAddTwoLoadsFromDiskAndFlattensToTheSameNetwork()
        {
            (ModuleLibrary library, Module increment) = Increments();
            (Composition chain, PortBinding binding) = Chain(library, increment, 2);

            var budget = new EvaluationBudget();

            Module promoted = Promotion.Promote(chain, ArithmeticParts.AddTwo(), binding, library, Origin, budget, log.Add).Module!;
            PartLibraryFiles.Save(library, folder);
            ModuleLibrary loaded = PartLibraryFiles.Load(folder);
            LibraryPart addTwo = loaded.PartFor("add 2")!.Part!;

            Assert.Equal(Json(chain.Flatten(library)), Json(addTwo.Part.Network));
            Assert.Equal(promoted.Part!.Part.Binding.Positions, addTwo.Part.Binding.Positions);
            Assert.Equal(promoted.Part.Cost, addTwo.Cost);
            Assert.Contains(log, line => line.StartsWith("Promoted the composition for add 2"));
            Assert.True(budget[EvaluationKind.ProofStep] > 0);
        }

        // The file names its children and wiring, so a change to a child shows in the child's file alone.
        [Fact]
        public void APromotedPartIsStoredAsItsChildrenAndWiringRatherThanANetwork()
        {
            (ModuleLibrary library, Module increment) = Increments();
            (Composition chain, PortBinding binding) = Chain(library, increment, 2);
            Promotion.Promote(chain, ArithmeticParts.AddTwo(), binding, library, Origin, new EvaluationBudget(), log.Add);

            PartLibraryFiles.Save(library, folder);
            string file = File.ReadAllText(Path.Combine(folder, "add-2.json"));

            Assert.Contains("\"Recipe\"", file);
            Assert.DoesNotContain("\"Network\"", file);
            Assert.Contains("\"Contract\": \"increment\"", file);
            Assert.Contains("\"2.out\"", file);
        }

        [Fact]
        public void ACompositeLargerThanTheLeafCapIsAccepted()
        {
            (ModuleLibrary library, Module increment) = Increments();
            (Composition chain, PortBinding binding) = Chain(library, increment, 5);

            Module? promoted = Promotion.Promote(chain, CatalogueEntry.Of(Specifications.AddConstant(5), FirstParts.Each(FirstParts.Values)).Contract, binding, library, Origin, new EvaluationBudget(), log.Add).Module;

            Assert.NotNull(promoted);
            Assert.True(promoted!.Body.Neurons.Count > ModuleLibrary.MaxModuleNeurons);
            Assert.True(promoted.Part!.IsComposite);
        }

        [Fact]
        public void ACompositionThatFailsTheContractIsNotPromoted()
        {
            (ModuleLibrary library, Module increment) = Increments();
            (Composition chain, PortBinding binding) = Chain(library, increment, 2);

            Promoted promoted = Promotion.Promote(chain, FirstParts.Named("increment"), binding, library, Origin, new EvaluationBudget(), log.Add);

            Assert.Equal(ContractRule.DoneOnce, Assert.IsType<Verdict.Failed>(promoted.Verdict).Counterexample.Rule);
            Assert.Null(promoted.Module);
            Assert.Single(library.Parts);
            Assert.Contains(log, line => line.Contains("fails the contract"));
        }

        // add 4 is two add 2s, each two increments, and its file sorts before the files it needs.
        [Fact]
        public void PartsBuiltFromPromotedPartsLoadAfterTheirChildren()
        {
            (ModuleLibrary library, Module increment) = Increments();
            (Composition two, PortBinding twoBinding) = Chain(library, increment, 2);
            Module addTwo = Promotion.Promote(two, ArithmeticParts.AddTwo(), twoBinding, library, Origin, new EvaluationBudget(), log.Add).Module!;
            (Composition four, PortBinding fourBinding) = Chain(library, addTwo, 2);
            Promotion.Promote(four, CatalogueEntry.Of(Specifications.AddConstant(4), FirstParts.Each(FirstParts.Values)).Contract, fourBinding, library, Origin, new EvaluationBudget(), log.Add);

            PartLibraryFiles.Save(library, folder);
            ModuleLibrary loaded = PartLibraryFiles.Load(folder);

            Assert.Equal(new[] { "add 2", "add 4", "increment" }, loaded.Parts.Select(module => module.Part!.Contract.Name).Order());
            Assert.Equal(
                Json(library.PartFor("add 4")!.Part!.Part.Network),
                Json(loaded.PartFor("add 4")!.Part!.Part.Network));
        }

        [Fact]
        public void APromotedPartWhoseChildIsMissingIsRefusedByName()
        {
            (ModuleLibrary library, Module increment) = Increments();
            (Composition chain, PortBinding binding) = Chain(library, increment, 2);
            Promotion.Promote(chain, ArithmeticParts.AddTwo(), binding, library, Origin, new EvaluationBudget(), log.Add);
            PartLibraryFiles.Save(library, folder);
            File.Delete(Path.Combine(folder, "increment.json"));

            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => PartLibraryFiles.Load(folder));

            Assert.Contains("'add-2.json' is built from parts the folder does not have: increment", refused.Message);
        }
    }
}
