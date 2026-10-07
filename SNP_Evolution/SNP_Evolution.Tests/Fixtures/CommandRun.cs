using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SnpEvolution.Cli;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Fixtures
{
    // Swaps the process-wide console, working directory and culture, so tests using it belong to ProcessStateCollection.
    internal sealed partial class CommandRun : IDisposable
    {
        // Pictures of a network, which only lay out what the .json and .txt files already hold.
        private static readonly string[] Drawings = { ".html", ".svg" };

        private readonly TempFolder folder = new TempFolder("snp-golden");

        public string Folder => folder.Path;

        // The .git marker makes the folder the root the commands find runs/ and parts/ from, wherever the temp folder lies.
        public CommandRun()
        {
            Directory.CreateDirectory(Path.Combine(Folder, ".git"));
        }

        public void CopyFolder(string repositoryFolder)
        {
            foreach (string file in Directory.GetFiles(Path.Combine(RepositoryFiles.Root, repositoryFolder)))
            {
                CopyFile(Path.Combine(repositoryFolder, Path.GetFileName(file)));
            }
        }

        public void CopyFile(string repositoryFile)
        {
            string target = Path.Combine(Folder, repositoryFile);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(RepositoryFiles.Root, repositoryFile), target);
        }

        // Saves a hand-built part as a library file, measured the way evolve-parts measures the parts it saves.
        public void Save(Part part, string file) =>
            File.WriteAllText(Path.Combine(Folder, file), PartLibraryFiles.ToJson(PartFixtures.Measured(part, new PartOrigin(0, HandBuiltParts.Origin, 0))));

        // The command line, exit code, output and every file the run left in the folder.
        public string Run(params string[] args)
        {
            (int exit, string printed, string errors) = RunInFolder(args);
            var text = new StringBuilder();
            text.Append("$ snp-evolution ").AppendJoin(' ', args.Select(arg => arg.Contains(' ') || arg.Contains(',') ? $"\"{arg}\"" : arg)).Append('\n');
            text.Append("exit ").Append(exit).Append('\n');
            text.Append("--- stdout\n").Append(printed);
            text.Append("--- stderr\n").Append(errors);
            AppendFiles(text);
            return Normalise(text.ToString());
        }

        private (int Exit, string Printed, string Errors) RunInFolder(string[] args)
        {
            string directory = Directory.GetCurrentDirectory();
            CultureInfo culture = CultureInfo.CurrentCulture;
            using var console = new ConsoleCapture();
            try
            {
                Directory.SetCurrentDirectory(Folder);
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                int exit = CommandLine.Run(args);
                return (exit, console.Printed, console.Errors);
            }
            finally
            {
                Directory.SetCurrentDirectory(directory);
                CultureInfo.CurrentCulture = culture;
            }
        }

        private void AppendFiles(StringBuilder text)
        {
            foreach (string file in Directory.GetFiles(Folder, "*", SearchOption.AllDirectories).Where(file => !Drawings.Contains(Path.GetExtension(file))).Order(StringComparer.Ordinal))
            {
                string content = File.ReadAllText(file);
                text.Append("--- file ").Append(Path.GetRelativePath(Folder, file).Replace('\\', '/')).Append('\n').Append(content);
                if (!content.EndsWith('\n'))
                {
                    text.Append("\n--- (no newline at end of file)\n");
                }
            }
        }

        // These parts of the text change from run to run or machine to machine.
        private string Normalise(string text)
        {
            // On macOS the temporary folder is reached through /private as well.
            foreach (string spelling in new[] { Path.Combine("/private", Folder.TrimStart('/')), Folder })
            {
                text = text.Replace(spelling, "<work>");
            }
            text = RunFolder().Replace(text, "runs/<run>");
            text = Seconds().Replace(text, "in <time> s");
            return NirToolsMissing().Replace(text, "$1<reason>");
        }

        public void Dispose() => folder.Dispose();

        [GeneratedRegex(@"runs[/\\]\d+")]
        private static partial Regex RunFolder();

        [GeneratedRegex(@"in \d+(\.\d+)? s\b")]
        private static partial Regex Seconds();

        [GeneratedRegex(@"(Not converted to NIR or co-simulated: ).*")]
        private static partial Regex NirToolsMissing();
    }
}
