using System.Text;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Golden
{
    // Parts on disk load only when their contract matches the catalogue's, so every contract, and what the bounded check
    // asks of it at each bound, is pinned here.
    public class ContractGoldenTests
    {
        private const int Bounds = 30;
        private const int CaseBounds = 4;

        [Fact]
        public void EveryCatalogueContractIsAsBefore()
        {
            var text = new StringBuilder();
            foreach (FirstPart part in FirstParts.All)
            {
                text.Append($"first part {part.Name} | {part.Ports} | {part.Goal} | {string.Join(", ", part.Contracts.Select(contract => contract.Name))}\n");
            }
            foreach (Contract contract in ArithmeticParts.Known.Concat(HandBuiltParts.All().Select(part => part.Contract)))
            {
                text.Append(Json.Write(contract).ReplaceLineEndings("\n")).Append('\n');
            }
            foreach (BenchmarkTask task in TaskSuite.Functions)
            {
                var function = (FunctionTask)task.Task;
                text.Append($"{function.Name}: {string.Join(" ", function.Examples.Select(example => $"{string.Join(",", example.Arguments)}->{example.Result}"))}\n");
            }
            GoldenFile.Check("contracts", text.ToString());
        }

        [Fact]
        public void EveryBoundedContractIsAsBefore()
        {
            var text = new StringBuilder();
            foreach (Contract contract in ArithmeticParts.Known)
            {
                text.Append("== ").Append(contract.Name).Append('\n');
                for (int bound = 0; bound <= Bounds; bound++)
                {
                    if (BoundedCheck.AtBound(contract, bound) is Contract atBound)
                    {
                        text.Append($"{bound}: latency {atBound.MaxLatency}");
                        if (bound <= CaseBounds)
                        {
                            text.Append(" cases ").AppendJoin(" ", atBound.Cases.Select(@case =>
                                $"{@case.Label(atBound.DataIn)}->{@case.Done}{string.Concat(@case.Outputs.Select(pair => $",{pair.Key}={pair.Value}"))}"));
                        }
                        text.Append('\n');
                    }
                }
            }
            GoldenFile.Check("bounded-contracts", text.ToString());
        }
    }
}
