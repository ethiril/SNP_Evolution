using SnpEvolution.Evolution.Contracts;

namespace SnpEvolution.Tests.Golden
{
    // Populations stay under the GPU's batch threshold, so a Mac with Metal runs these on the CPU exactly as Linux does.
    [Collection(GoldenCollection.Name)]
    public class CommandGoldenTests
    {
        [Fact]
        public void Evolve()
        {
            using var run = new CommandRun();
            GoldenFile.Check("evolve", run.Run("evolve", "--target", "2,4,6", "--seed", "1", "--population", "20", "--generations", "125"));
        }

        [Fact]
        public void EvolveParts()
        {
            using var run = new CommandRun();
            GoldenFile.Check("evolve-parts", run.Run("evolve-parts", "--only", "delay 2", "--seed", "1", "--library", "library"));
        }

        [Fact]
        public void Compile()
        {
            using var run = new CommandRun();
            GoldenFile.Check("compile", run.Run("compile", "--target", "1,1,2,3,5,8,13", "--seed", "1", "--shrink", "20"));
        }

        [Fact]
        public void Compose()
        {
            using var run = new CommandRun();
            run.CopyFolder("parts");
            GoldenFile.Check("compose", run.Run("compose", "--task", "Contract multiply", "--hand-built", "on", "--seed", "1", "--library", "parts",
                "--evaluations", "100", "--population", "10"));
        }

        [Fact]
        public void VerifyTheHandBuiltAdd()
        {
            using var run = new CommandRun();
            run.Save(ReferenceParts.Add(), "add.json");
            GoldenFile.Check("verify-add", run.Run("verify", "--part", "add.json", "--bound", "6"));
        }

        // NIR takes only hardware-profile networks, so its delay comes from parts-profile/.
        public static TheoryData<string, string> Exports => new TheoryData<string, string>
        {
            { "export-verilog", "parts/delay-2.json" }, { "export-verilog", Register },
            { "export-nir", "parts-profile/delay-2.json" }, { "export-nir", Register },
            { "export-uppaal", "parts/delay-2.json" }, { "export-uppaal", Register },
        };

        private const string Register = "register.json";

        [Theory]
        [MemberData(nameof(Exports))]
        public void Export(string command, string part)
        {
            using var run = new CommandRun();
            if (part == Register)
            {
                run.Save(HandBuiltParts.All().Single(each => each.Contract.Name == "register"), Register);
            }
            else
            {
                run.CopyFile(part);
            }
            string[] check = command == "export-nir" ? Array.Empty<string>() : new[] { "--check", "off" };
            GoldenFile.Check($"{command}-{Path.GetFileNameWithoutExtension(part)}", run.Run(new[] { command, "--part", part, "--out", "export" }.Concat(check).ToArray()));
        }
    }
}
