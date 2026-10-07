using System.Xml.Linq;

namespace SnpEvolution.Tests.Layering
{
    // The build keeps a project from using one it does not reference, so the layers hold if no project references one
    // above it, and each project's code sits in its own namespace.
    public class ProjectLayerTests
    {
        public static TheoryData<string> Projects() => new(Layers.Projects);

        [Fact]
        public void TheSolutionHoldsTheLayersAndTheTests()
        {
            XDocument solution = XDocument.Load(Path.Combine(RepositoryFiles.Solution, "SNP_Evolution.slnx"));

            IEnumerable<string> listed = solution.Descendants("Project").Select(project => project.Attribute("Path")!.Value.Split('/')[0]);

            Assert.Equal(Layers.Projects.Append("SNP_Evolution.Tests").Order(), listed.Order());
        }

        [Theory]
        [MemberData(nameof(Projects))]
        public void NoProjectReferencesOneAboveIt(string project)
        {
            XDocument file = XDocument.Load(Path.Combine(Layers.Folder(project), project + ".csproj"));

            IEnumerable<string> above = file.Descendants("ProjectReference")
                .Select(reference => Path.GetFileNameWithoutExtension(reference.Attribute("Include")!.Value.Replace('\\', '/')))
                .Where(referenced => Layers.IsAtOrAbove(referenced, project));

            Assert.Empty(above);
        }

        [Theory]
        [MemberData(nameof(Projects))]
        public void EachProjectsCodeIsInItsOwnNamespace(string project)
        {
            string own = "SnpEvolution." + project;

            IEnumerable<string> elsewhere = Layers.Assembly(project).GetTypes()
                .Where(type => type.DeclaringType == null && type.Namespace != null && type.Namespace.StartsWith("SnpEvolution", StringComparison.Ordinal))
                .Where(type => type.Namespace != own && !type.Namespace!.StartsWith(own + ".", StringComparison.Ordinal))
                .Select(type => type.FullName!);

            Assert.Empty(elsewhere);
        }

        // The compiler drops a reference whose types go unused, so this catches what the project files cannot show: a raw assembly reference.
        [Fact]
        public void TheCompiledAssembliesReferenceOnlyLayersBelow()
        {
            IEnumerable<string> upwards = Layers.Projects.SelectMany(project => Layers.Assembly(project).GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(name => name.StartsWith("SnpEvolution.", StringComparison.Ordinal))
                .Where(name => Layers.IsAtOrAbove(name["SnpEvolution.".Length..], project))
                .Select(name => $"{project} -> {name}"));

            Assert.Empty(upwards);
        }

        [Theory]
        [InlineData("Model", "Simulation", false)]
        [InlineData("Simulation", "Simulation", true)]
        [InlineData("Cli", "Application", true)]
        [InlineData("Plugins", "Cli", true)]
        public void AReferenceCountsAsAboveUnlessItIsAnEarlierLayer(string referenced, string project, bool above) =>
            Assert.Equal(above, Layers.IsAtOrAbove(referenced, project));
    }
}
