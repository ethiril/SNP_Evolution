using System.Reflection;

namespace SnpEvolution.Tests.Layering
{
    // Lowest first, so each project may reference only the ones before it.
    internal static class Layers
    {
        private static readonly string[] Order = { "Model", "Simulation", "Specs", "Compilation", "Search", "Storage", "Export", "Application", "Cli" };

        public static IReadOnlyList<string> Projects => Order;

        // An unknown project counts as above, so a reference to it is refused rather than missed.
        public static bool IsAtOrAbove(string referenced, string project) =>
            Array.IndexOf(Order, referenced) is var at && (at < 0 || at >= Array.IndexOf(Order, project));

        public static string Folder(string project) => Path.Combine(RepositoryFiles.Solution, project);

        public static Assembly Assembly(string project) => System.Reflection.Assembly.Load("SnpEvolution." + project);
    }
}
