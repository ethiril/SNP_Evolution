using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests
{
    // Files kept in the repository, found from the test assembly rather than the working directory, so a test that
    // changes the working directory does not move them.
    internal static class RepositoryFiles
    {
        public static string Root { get; } = FindRoot();

        // A part saved in parts/ or parts-profile/.
        public static string PartFile(string folder, string file) => Path.Combine(Root, folder, file);

        public static LibraryPart ReadPart(string folder, string file) => PartLibraryFiles.Read(File.ReadAllText(PartFile(folder, file)), file);

        private static string FindRoot()
        {
            for (string? folder = AppContext.BaseDirectory; folder != null; folder = Path.GetDirectoryName(folder))
            {
                // .git is a file in a worktree.
                if (Directory.Exists(Path.Combine(folder, ".git")) || File.Exists(Path.Combine(folder, ".git")))
                {
                    return folder;
                }
            }
            throw new DirectoryNotFoundException($"No repository above {AppContext.BaseDirectory}.");
        }
    }
}
