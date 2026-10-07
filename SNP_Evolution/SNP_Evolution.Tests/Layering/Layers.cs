using System.Reflection;

namespace SnpEvolution.Tests.Layering
{
    // The program's projects, lowest first. Each is the namespace SnpEvolution.<name> in the folder of that name, and
    // may reference only the projects before it.
    internal static class Layers
    {
        public static readonly string[] Projects = { "Model", "Simulation", "Specs", "Compilation", "Search", "Storage", "Export", "Application", "Cli" };

        public static string Folder(string project) => Path.Combine(RepositoryFiles.Solution, project);

        public static Assembly Assembly(string project) => System.Reflection.Assembly.Load("SnpEvolution." + project);
    }
}
