using SnpEvolution.Networks;

namespace SnpEvolution.Tests.Networks
{
    public class NetworkPageTests
    {
        [Fact]
        public void LaysOutEveryNeuronWithItsRulesAndSynapses()
        {
            string html = NetworkPage.Html(ReferenceNetworks.NaturalNumbers(), "Natural numbers");

            Assert.StartsWith("<!doctype html>", html);
            Assert.Contains("<title>Natural numbers</title>", html);
            Assert.Contains("class=\"neuron\" data-index=\"0\" data-targets=\"2 3 4\"", html);
            Assert.Contains("class=\"neuron out\" data-index=\"3\" data-targets=\"\"", html);
            Assert.Contains("<div class=\"rule\">aaa -&gt; forget</div>", html);
        }
    }
}
