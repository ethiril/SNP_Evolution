using Newtonsoft.Json;

namespace SnpEvolution.Specs.Parts
{
    // UpTo is the largest bound N for which every input with every value at most N meets the contract, -1 when not even
    // N = 0 is proven. AllInputs says the bound covers every input there is, as it does for a part with no data in-ports
    // or only binary ones. Stopped says why the check went no further; FailsAt names the input of a counterexample.
    public sealed record ProvenBound(int UpTo, bool AllInputs, StopReason Stopped, [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] string? FailsAt = null)
    {
        public override string ToString() => AllInputs ? "proven for every input" : UpTo < 0 ? $"not proven ({Stopped})" : $"proven up to {UpTo} ({Stopped})";
    }
}
