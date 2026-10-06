using SnpEvolution.Export;

namespace SnpEvolution.Tests.Export
{
    // A test that runs Icarus Verilog, skipped with the reason when it is not installed, as on CI.
    public sealed class IverilogFactAttribute : FactAttribute
    {
        public IverilogFactAttribute()
        {
            if (!Iverilog.IsInstalled)
            {
                Skip = Iverilog.Missing;
            }
        }
    }

    // A test that runs tools/snp_nir.py, skipped with the reason when Python or its packages are missing.
    public sealed class NirFactAttribute : FactAttribute
    {
        public NirFactAttribute()
        {
            if (NirExporter.Unavailable() is string reason)
            {
                Skip = reason;
            }
        }
    }

    // A test that runs Uppaal's verifyta, skipped with the reason when it is not installed.
    public sealed class VerifytaFactAttribute : FactAttribute
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
