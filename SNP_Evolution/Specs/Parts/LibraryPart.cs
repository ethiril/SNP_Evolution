using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SnpEvolution.Specs.Contracts;

namespace SnpEvolution.Specs.Parts
{
    // Where a library part came from: the seed its contract was evolved with, the run that found it, and the networks
    // scored to find, shrink and verify it. A part compiled from a register program keeps the program and the size the
    // compiler made it, before shrinking, so a reader can tell which parts came from the compiler.
    public sealed record PartOrigin(
        int Seed,
        string Run,
        long Evaluations,
        [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] string? Program = null,
        [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] HardwareCost? Compiled = null);

    // A verified part as the library keeps it, with what measuring it on its contract gave. Proven is the bound a bounded
    // check reached, null when it has not been checked.
    public sealed record LibraryPart(Part Part, HardwareCost Cost, int Latency, string Behaviour, PartOrigin Origin, PartRecipe? Recipe = null, ProvenBound? Proven = null)
    {
        public bool IsComposite => Recipe != null;

        public Contract Contract => Part.Contract;
    }
}
