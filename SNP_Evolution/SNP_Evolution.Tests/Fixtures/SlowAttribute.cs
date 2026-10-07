using Xunit.Abstractions;
using Xunit.Sdk;

namespace SnpEvolution.Tests.Fixtures
{
    // Marks a test that runs a search, a bounded check with a time limit or an external tool, so the fast set
    // (dotnet test --filter Speed!=slow) can leave it out.
    [TraitDiscoverer(SpeedDiscoverer.TypeName, SpeedDiscoverer.AssemblyName)]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class SlowAttribute : Attribute, ITraitAttribute
    {
    }

    // Gives Speed=slow to every attribute that names it, including the facts that run an external tool.
    public sealed class SpeedDiscoverer : ITraitDiscoverer
    {
        public const string TypeName = "SnpEvolution.Tests.Fixtures." + nameof(SpeedDiscoverer);
        public const string AssemblyName = "SNP_Evolution.Tests";

        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            yield return new KeyValuePair<string, string>("Speed", "slow");
        }
    }
}
