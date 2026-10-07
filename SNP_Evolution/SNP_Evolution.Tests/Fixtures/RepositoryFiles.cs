using SnpEvolution.Application;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Fixtures
{
    // Found from the test assembly, not the working directory, which some tests change.
    internal static class RepositoryFiles
    {
        public static string Root { get; } = RunFolders.RepositoryRootAbove(AppContext.BaseDirectory)
            ?? throw new DirectoryNotFoundException($"No repository above {AppContext.BaseDirectory}.");

        // The folder holding the solution and its projects.
        public static string Solution => Path.Combine(Root, "SNP_Evolution");

        public static string PartFile(string folder, string file) => Path.Combine(Root, folder, file);

        public static LibraryPart ReadPart(string folder, string file) => PartLibraryFiles.Read(File.ReadAllText(PartFile(folder, file)), file);

        public static Part Part(string folder, string file) => ReadPart(folder, file).Part;
    }
}
