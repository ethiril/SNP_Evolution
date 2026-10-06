using SnpEvolution.Export;
using Xunit.Sdk;

namespace SnpEvolution.Tests.Export
{
    // A test that runs Icarus Verilog, skipped with the reason when it is not installed; slow, as every tool test is.
    [TraitDiscoverer(SpeedDiscoverer.TypeName, SpeedDiscoverer.AssemblyName)]
    public sealed class IverilogFactAttribute : FactAttribute, ITraitAttribute
    {
        public IverilogFactAttribute()
        {
            if (!Iverilog.IsInstalled)
            {
                Skip = Iverilog.Missing;
            }
        }
    }

    // A test that runs tools/snp_nir.py, skipped with the reason when Python or its packages are missing; slow.
    [TraitDiscoverer(SpeedDiscoverer.TypeName, SpeedDiscoverer.AssemblyName)]
    public sealed class NirFactAttribute : FactAttribute, ITraitAttribute
    {
        public NirFactAttribute()
        {
            if (NirExporter.Unavailable() is string reason)
            {
                Skip = reason;
            }
        }
    }

    // A test that runs Uppaal's verifyta, skipped with the reason when it is not installed; slow.
    [TraitDiscoverer(SpeedDiscoverer.TypeName, SpeedDiscoverer.AssemblyName)]
    public sealed class VerifytaFactAttribute : FactAttribute, ITraitAttribute
    {
        public VerifytaFactAttribute()
        {
            if (!Verifyta.IsInstalled)
            {
                Skip = Verifyta.Missing;
            }
        }
    }
}
