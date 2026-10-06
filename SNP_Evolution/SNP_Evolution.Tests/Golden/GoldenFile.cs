namespace SnpEvolution.Tests.Golden
{
    // SNP_UPDATE_GOLDEN=1 rewrites the expected files instead of comparing, for a change the pull request explains.
    internal static class GoldenFile
    {
        private const string UpdateFlag = "SNP_UPDATE_GOLDEN";

        private static string Folder => Path.Combine(RepositoryFiles.Root, "SNP_Evolution", "SNP_Evolution.Tests", "Golden", "Expected");

        public static void Check(string name, string actual)
        {
            string path = Path.Combine(Folder, name + ".txt");
            if (Environment.GetEnvironmentVariable(UpdateFlag) == "1")
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(path, actual);
                return;
            }
            Assert.True(File.Exists(path), $"There is no golden file {name}.txt; run the tests with {UpdateFlag}=1 to write it.");
            // Git may check the file out with Windows line endings.
            string expected = File.ReadAllText(path).ReplaceLineEndings("\n");
            if (FirstDifference(expected, actual) is string difference)
            {
                Assert.Fail($"{name}.txt differs from this run. {difference}\n" +
                    $"If the change is intended, rerun with {UpdateFlag}=1 and say why in the pull request.");
            }
        }

        public static string? FirstDifference(string expected, string actual)
        {
            string[] expectedLines = expected.Split('\n');
            string[] actualLines = actual.Split('\n');
            for (int line = 0; line < Math.Max(expectedLines.Length, actualLines.Length); line++)
            {
                string? was = line < expectedLines.Length ? expectedLines[line] : null;
                string? now = line < actualLines.Length ? actualLines[line] : null;
                if (was != now)
                {
                    return $"First difference at line {line + 1}:\n  expected: {was ?? "(end of file)"}\n  actual:   {now ?? "(end of file)"}";
                }
            }
            return null;
        }
    }
}
