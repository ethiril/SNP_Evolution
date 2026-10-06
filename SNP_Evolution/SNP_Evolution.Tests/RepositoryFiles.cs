using SnpEvolution.Cli;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests
{
    // Found from the test assembly, not the working directory, which some tests change.
    internal static class RepositoryFiles
    {
        public static string Root { get; } = Settings.RepositoryRootAbove(AppContext.BaseDirectory)
            ?? throw new DirectoryNotFoundException($"No repository above {AppContext.BaseDirectory}.");

        public static string PartFile(string folder, string file) => Path.Combine(Root, folder, file);

        public static LibraryPart ReadPart(string folder, string file) => PartLibraryFiles.Read(File.ReadAllText(PartFile(folder, file)), file);
    }
}
