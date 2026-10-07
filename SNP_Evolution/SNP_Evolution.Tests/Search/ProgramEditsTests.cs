using SnpEvolution.Compilation;
using SnpEvolution.Search;

namespace SnpEvolution.Tests.Search
{
    public class ProgramEditsTests
    {
        [Fact]
        public void AnEditNeverLeavesAProgramWithoutInstructions()
        {
            var edits = new ProgramEdits(16, 4, new Random(1));
            RegisterProgram single = RegisterProgram.Parse("HALT");

            Assert.All(Enumerable.Range(0, 300), _ => Assert.NotEmpty(edits.Mutate(single).Instructions));
        }
    }
}
