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

        // Fresh and edited, a function program's ADDs never choose, its SUBs reach r0, and its HALTs reach both done ports.
        [Fact]
        public void FunctionProgramEditsNeverMakeAChoice()
        {
            var edits = ProgramEdits.ForFunctions(16, 4, new Random(1), dones: 2);
            List<RegisterProgram> fresh = Enumerable.Range(0, 300).Select(_ => edits.RandomProgram()).ToList();
            List<Instruction> made = fresh.SelectMany(program => Enumerable.Range(0, 5).Aggregate(program, (current, _) => edits.Mutate(current)).Instructions).ToList();

            Assert.All(made.Where(instruction => instruction.Operation == Operation.Add), add => Assert.Equal(add.Next, add.Else));
            Assert.Contains(made, instruction => instruction.Operation == Operation.Sub && instruction.Register == 0);
            Assert.All(new[] { fresh.SelectMany(program => program.Instructions), made }, instructions =>
                Assert.Equal(new[] { 0, 1 }, instructions.Where(instruction => instruction.Operation == Operation.Halt).Select(instruction => instruction.Register).Distinct().Order()));
        }
    }
}
