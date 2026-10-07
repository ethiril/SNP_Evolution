using System.Text.RegularExpressions;
using SnpEvolution.Cli;

namespace SnpEvolution.Tests.Cli
{
    // Results in the README and RESEARCH.md are reproduced by the command lines quoted there, so each must still parse,
    // and the README's command table must list what the registry declares.
    public partial class CommandDocsTests
    {
        private static string Read(string file) => File.ReadAllText(Path.Combine(RepositoryFiles.Root, file)).ReplaceLineEndings("\n");

        [Fact]
        public void TheReadmeCommandTableMatchesTheRegistry()
        {
            string table = CommandRegistry.MarkdownTable().ReplaceLineEndings("\n");

            Assert.True(Read("README.md").Contains(table), "Replace the README's command table with:\n" + table);
        }

        public static TheoryData<string, string> QuotedCommandLines()
        {
            var lines = new TheoryData<string, string>();
            foreach (string file in new[] { "README.md", "RESEARCH.md" })
            {
                string text = Read(file);
                IEnumerable<string> quoted = CodeLine().Matches(text).Select(match => match.Groups["line"].Value)
                    .Concat(InlineCode().Matches(text).Select(match => match.Groups["line"].Value));
                foreach (string line in quoted.Select(line => Comment().Replace(line, "").Trim()).Distinct())
                {
                    if (Words(line) is { Count: > 0 } words && CommandRegistry.Find(words[0]) != null)
                    {
                        lines.Add(file, line);
                    }
                }
            }
            return lines;
        }

        [Theory]
        [MemberData(nameof(QuotedCommandLines))]
        public void EveryQuotedCommandLineParses(string file, string line)
        {
            List<string> words = Words(line);

            CommandArgs? parsed = CommandArgs.Parse(CommandRegistry.Find(words[0])!, words, out string error);

            Assert.True(parsed != null, $"{file}: {line}\n{error}");
        }

        [Fact]
        public void TheDocumentsQuoteEveryCommandButTheListings()
        {
            HashSet<string> quoted = QuotedCommandLines().Select(row => Words((string)row[1])[0]).ToHashSet();

            List<string> unquoted = CommandRegistry.All.Select(command => command.Name).Where(name => !quoted.Contains(name) && name is not "tasks" and not "algorithms" and not "advise").ToList();

            Assert.True(unquoted.Count == 0, "No example of: " + string.Join(", ", unquoted));
        }

        // The words of a shell line, with quotes removed from quoted words.
        private static List<string> Words(string line) => ShellWord().Matches(line).Select(match => match.Groups["quoted"].Success ? match.Groups["quoted"].Value : match.Value).ToList();

        // A command after snp-evolution or dotnet run's --, at the start of a line.
        [GeneratedRegex(@"^(?:snp-evolution|dotnet run [^\n]*? --) (?<line>[^\n]+)$", RegexOptions.Multiline)]
        private static partial Regex CodeLine();

        [GeneratedRegex(@"`(?<line>[a-z-]+ --[^`]+)`")]
        private static partial Regex InlineCode();

        [GeneratedRegex(@"\s+#.*$")]
        private static partial Regex Comment();

        [GeneratedRegex(@"""(?<quoted>[^""]*)""|\S+")]
        private static partial Regex ShellWord();
    }
}
