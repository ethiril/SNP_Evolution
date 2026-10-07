namespace SnpEvolution.Tests.Layering
{
    // The build keeps projects from depending on each other in a cycle, but not the namespaces inside one project, so
    // no namespace may depend on itself through others.
    public class NamespaceCycleTests
    {
        [Fact]
        public void NoNamespaceDependsOnItself()
        {
            Dictionary<string, SortedSet<string>> graph = NamespaceGraph.Build();

            List<List<string>> cycles = NamespaceGraph.Cycles(graph);

            Assert.True(cycles.Count == 0, "Namespaces depend on each other in a cycle:\n" + string.Join("\n", cycles.Select(cycle =>
                string.Join("\n", cycle.SelectMany(from => graph[from].Where(cycle.Contains).Select(to => $"  {from} -> {to}: {NamespaceGraph.Reasons(from, to)}"))))));
        }

        [Fact]
        public void FindsACycleWhereThereIsOne()
        {
            var graph = new Dictionary<string, SortedSet<string>>
            {
                ["A"] = new SortedSet<string> { "B" },
                ["B"] = new SortedSet<string> { "C" },
                ["C"] = new SortedSet<string> { "A" },
                ["D"] = new SortedSet<string> { "A" },
            };

            Assert.Equal(new[] { "A", "B", "C" }, Assert.Single(NamespaceGraph.Cycles(graph)));
        }
    }
}
