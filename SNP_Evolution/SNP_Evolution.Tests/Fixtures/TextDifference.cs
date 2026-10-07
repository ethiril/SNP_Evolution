namespace SnpEvolution.Tests.Fixtures
{
    internal static class TextDifference
    {
        // The first line where two texts differ, or null when they are the same.
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
