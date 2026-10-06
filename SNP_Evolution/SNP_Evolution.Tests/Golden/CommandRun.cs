using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SnpEvolution.Cli;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Golden
{
    // Runs commands in-process in an empty working folder outside the repository, and writes down everything a run did:
    // its exit code, what it printed and every file it left in the folder. The console, working directory and culture
    // are process-wide, so tests using this belong to GoldenCollection, which runs alone.
    internal sealed partial class CommandRun : IDisposable
    {
        // Pictures of a network, which only lay out what the .json and .txt files already hold.
        private static readonly string[] Drawings = { ".html", ".svg" };

        public string Folder { get; } = Path.Combine(Path.GetTempPath(), "snp-golden-" + Guid.NewGuid().ToString("N"));

        // The .git marker makes the folder the root the commands find runs/ and parts/ from, wherever the temporary
        // folder lies.
        public CommandRun()
        {
            Directory.CreateDirectory(Path.Combine(Folder, ".git"));
        }

        // Copies a folder of the repository, such as parts/, into the working folder under the same name.
        public void CopyFolder(string repositoryFolder)
        {
            foreach (string file in Directory.GetFiles(Path.Combine(RepositoryFiles.Root, repositoryFolder)))
            {
                CopyFile(Path.Combine(repositoryFolder, Path.GetFileName(file)));
            }
        }

        // Copies a file of the repository, such as parts/delay-2.json, to the same path in the working folder.
        public void CopyFile(string repositoryFile)
        {
            string target = Path.Combine(Folder, repositoryFile);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(RepositoryFiles.Root, repositoryFile), target);
        }

        // Saves a hand-built part as a library file, measured the way evolve-parts measures the parts it saves.
        public void Save(Part part, string file) =>
            File.WriteAllText(Path.Combine(Folder, file), PartLibraryFiles.ToJson(LibraryPart.Of(part, PartEvolution.Measure(part), new PartOrigin(0, HandBuiltParts.Origin, 0))));

        public string Run(params string[] args)
        {
            TextWriter output = Console.Out, error = Console.Error;
            string directory = Directory.GetCurrentDirectory();
            CultureInfo culture = CultureInfo.CurrentCulture;
            var printed = new StringWriter { NewLine = "\n" };
            var errors = new StringWriter { NewLine = "\n" };
            int exit;
            try
            {
                Console.SetOut(printed);
                Console.SetError(errors);
                Directory.SetCurrentDirectory(Folder);
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                exit = CommandLine.Run(args);
            }
            finally
            {
                Console.SetOut(output);
                Console.SetError(error);
                Directory.SetCurrentDirectory(directory);
                CultureInfo.CurrentCulture = culture;
            }
            var text = new StringBuilder();
            text.Append("$ snp-evolution ").AppendJoin(' ', args.Select(arg => arg.Contains(' ') || arg.Contains(',') ? $"\"{arg}\"" : arg)).Append('\n');
            text.Append("exit ").Append(exit).Append('\n');
            text.Append("--- stdout\n").Append(printed);
            text.Append("--- stderr\n").Append(errors);
            foreach (string file in Directory.GetFiles(Folder, "*", SearchOption.AllDirectories).Where(file => !Drawings.Contains(Path.GetExtension(file))).Order(StringComparer.Ordinal))
            {
                string content = File.ReadAllText(file);
                text.Append("--- file ").Append(Path.GetRelativePath(Folder, file).Replace('\\', '/')).Append('\n').Append(content);
                if (!content.EndsWith('\n'))
                {
                    text.Append("\n--- (no newline at end of file)\n");
                }
            }
            return Normalise(text.ToString());
        }

        // The working folder, the timestamp naming a run's folder, how long a proof took and which NIR tools the machine
        // lacks change from run to run.
        private string Normalise(string text)
        {
            // On macOS the temporary folder is reached through /private as well.
            foreach (string folder in new[] { Path.Combine("/private", Folder.TrimStart('/')), Folder })
            {
                text = text.Replace(folder, "<work>");
            }
            text = RunFolder().Replace(text, "runs/<run>");
            text = Seconds().Replace(text, "in <time> s");
            return NirToolsMissing().Replace(text, "$1<reason>");
        }

        public void Dispose()
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, recursive: true);
            }
        }

        [GeneratedRegex(@"runs[/\\]\d+")]
        private static partial Regex RunFolder();

        [GeneratedRegex(@"in \d+(\.\d+)? s\b")]
        private static partial Regex Seconds();

        [GeneratedRegex(@"(Not converted to NIR or co-simulated: ).*")]
        private static partial Regex NirToolsMissing();
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class GoldenCollection
    {
        public const string Name = "Golden runs";
    }
}
