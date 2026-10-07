namespace SnpEvolution.Tests.Layering
{
    // The menu and the command line sit on top: the run services and everything under them work without them, so a
    // new interface or a test can call the services directly.
    public class InterfaceLayerTests
    {
        private const string Interface = "SnpEvolution.Cli";

        // Program starts the menu or the command line, so only it may.
        private static readonly string[] Entry = { "SnpEvolution", Interface };

        [Fact]
        public void NothingBelowTheInterfaceDependsOnIt()
        {
            Dictionary<string, SortedSet<string>> graph = NamespaceGraph.Build();

            List<string> dependents = graph.Where(node => !Entry.Contains(node.Key) && node.Value.Contains(Interface)).Select(node => node.Key).ToList();

            Assert.True(dependents.Count == 0, "These depend on the interface:\n" + string.Join("\n", dependents.Select(from => $"  {from}: {NamespaceGraph.Reasons(from, Interface)}")));
        }

        [Fact]
        public void TheApplicationLayerIsInTheGraph() =>
            Assert.Contains("SnpEvolution.Application", NamespaceGraph.Build()[Interface]);
    }
}
