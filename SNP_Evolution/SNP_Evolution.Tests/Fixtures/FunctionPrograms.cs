using SnpEvolution.Compilation;
using SnpEvolution.Search;
using SnpEvolution.Specs.Contracts;

namespace SnpEvolution.Tests.Fixtures
{
    // Hand-written function programs for count contracts: inputs in the first registers, outputs after them. They are
    // tests of the route, not seeds, so nothing outside the tests reads them.
    public static class FunctionPrograms
    {
        public static IReadOnlyDictionary<string, string> Texts { get; } = new Dictionary<string, string>
        {
            ["register"] = "SUB r0 -> 1 else 2\nADD r1 -> 0\nHALT",
            ["increment"] = "SUB r0 -> 1 else 2\nADD r1 -> 0\nADD r1 -> 3\nHALT",
            ["double"] = "SUB r0 -> 1 else 3\nADD r1 -> 2\nADD r1 -> 0\nHALT",
            ["fan-out"] = "SUB r0 -> 1 else 3\nADD r1 -> 2\nADD r2 -> 0\nHALT",
            ["add"] = "SUB r0 -> 1 else 2\nADD r2 -> 0\nSUB r1 -> 3 else 4\nADD r2 -> 2\nHALT",
            ["zero test"] = "SUB r0 -> 1 else 3\nSUB r0 -> 1 else 2\nHALT nonzero\nHALT zero",
        };

        public static FunctionProgram For(string contractName)
        {
            Contract contract = FirstParts.Named(contractName);
            FunctionProgram layout = FunctionScoring.Layout(contract, new RegisterProgram(0, Array.Empty<Instruction>()));
            return FunctionProgram.Parse(Texts[contractName], layout.Inputs, layout.Outputs, layout.Dones);
        }
    }
}
