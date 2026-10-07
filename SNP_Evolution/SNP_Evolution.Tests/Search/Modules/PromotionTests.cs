using SnpEvolution.Search.Modules;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Specs.Verification;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.Fixtures.CompositionFixtures;

namespace SnpEvolution.Tests.Search.Modules
{
    public sealed class PromotionTests : IDisposable
    {
        private static readonly PartOrigin Origin = new PartOrigin(1, "a test", 0);

        private readonly TempFolder temp = new TempFolder("promotion");
        private readonly List<string> log = new List<string>();

        public void Dispose() => temp.Dispose();

        [Fact]
        public void TheTwoIncrementChainPromotedAsAddTwoLoadsFromDiskAndFlattensToTheSameNetwork()
        {
            (ModuleLibrary library, Module increment) = Increments();
            (Composition chain, PortBinding binding) = ChainOfCopies(library, increment, 2);
            var budget = new EvaluationBudget();

            Module promoted = Promotion.Promote(chain, ArithmeticParts.AddTwo(), binding, library, Origin, budget, log.Add).Module!;
            PartLibraryFiles.Save(library, temp.Path);
            ModuleLibrary loaded = PartLibraryFiles.Load(temp.Path);
            LibraryPart addTwo = loaded.PartFor("add 2")!.Part!;

            Assert.Equal(NetworkFiles.ToJson(chain.Flatten(library)), NetworkFiles.ToJson(addTwo.Part.Network));
            Assert.Equal(promoted.Part!.Part.Binding.Positions, addTwo.Part.Binding.Positions);
            Assert.Equal(promoted.Part.Cost, addTwo.Cost);
            Assert.Contains(log, line => line.StartsWith("Promoted the composition for add 2"));
            Assert.True(budget[EvaluationKind.ProofStep] > 0);
        }

        [Fact]
        public void ACompositeLargerThanTheLeafCapIsAccepted()
        {
            (ModuleLibrary library, Module increment) = Increments();
            (Composition chain, PortBinding binding) = ChainOfCopies(library, increment, 5);

            Module? promoted = Promotion.Promote(chain, CatalogueEntry.Of(Specifications.AddConstant(5), FirstParts.Each(FirstParts.Values)).Contract, binding, library, Origin, new EvaluationBudget(), log.Add).Module;

            Assert.NotNull(promoted);
            Assert.True(promoted!.Body.Neurons.Count > ModuleLibrary.MaxModuleNeurons);
            Assert.True(promoted.Part!.IsComposite);
        }

        [Fact]
        public void ACompositionThatFailsTheContractIsNotPromoted()
        {
            (ModuleLibrary library, Module increment) = Increments();
            (Composition chain, PortBinding binding) = ChainOfCopies(library, increment, 2);

            Promoted promoted = Promotion.Promote(chain, FirstParts.Named("increment"), binding, library, Origin, new EvaluationBudget(), log.Add);

            Assert.Equal(ContractRule.Values, Assert.IsType<Verdict.Failed>(promoted.Verdict).Counterexample.Rule);
            Assert.Null(promoted.Module);
            Assert.Single(library.Parts);
            Assert.Contains(log, line => line.Contains("fails the contract"));
        }
    }
}
