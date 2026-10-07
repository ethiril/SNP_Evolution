using SnpEvolution.Model;
using SnpEvolution.Specs.Parts;

namespace SnpEvolution.Specs.Verification
{
    // A network run on every case of a contract on the exhaustive engine. Latency is the slowest case's, from the step
    // start reaches the part to the step done fires; Behaviour is what ContractTask.Behaviour reads. Verdict passes when
    // every check of every case passed on every computation, which needs every case to have been followed exactly;
    // Description names the checks that failed.
    public sealed record PartMeasurement(Network Network, HardwareCost Cost, int Latency, string Behaviour, Verdict Verdict, string Description)
    {
        public LibraryPart ToLibraryPart(Part part, PartOrigin origin) => new LibraryPart(part, Cost, Latency, Behaviour, origin);
    }
}
