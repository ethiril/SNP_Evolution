using System.Xml.Linq;
using SnpEvolution.Model;

namespace SnpEvolution.Tests.Model
{
    public class NetworkGraphTests
    {
        [Fact]
        public void DrawsEveryNeuronAndSynapseAsValidSvg()
        {
            Network network = ReferenceNetworks.NaturalNumbers();

            XDocument svg = XDocument.Parse(NetworkGraph.Svg(network));
            XNamespace ns = "http://www.w3.org/2000/svg";

            Assert.Equal(network.Neurons.Count + 1, svg.Descendants(ns + "rect").Count());
            Assert.Equal(network.SynapseCount, svg.Descendants(ns + "path").Count(path => (string?)path.Attribute("stroke") == "#444"));
            Assert.Contains(svg.Descendants(ns + "text"), text => text.Value == "n4 (out)");
            Assert.Contains(svg.Descendants(ns + "text"), text => text.Value == "aa -> a");
        }
    }
}
