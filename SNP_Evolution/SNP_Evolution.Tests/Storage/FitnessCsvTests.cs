using System.Globalization;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Storage
{
    public class FitnessCsvTests
    {
        [Fact]
        public void FormatsOneLinePerGenerationRegardlessOfCulture()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            try
            {
                string csv = FitnessCsv.Format(new[] { new[] { 0.5f, 1f }, Array.Empty<float>() });

                Assert.Equal("0.5,1\n\n", csv);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }
}
