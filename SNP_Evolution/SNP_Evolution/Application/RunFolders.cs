using System;
using System.IO;

namespace SnpEvolution.Application
{
    // Where runs write their files: runs/ beside parts/ at the root of the repository the program runs in, or in the
    // working directory outside one, and ignored by git so runs never land in the source tree.
    internal static class RunFolders
    {
        public const string RunsFolder = "runs";

        public static string NewOutputFolder() =>
            Path.Combine(WorkingRoot(), RunsFolder, (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond).ToString());

        // The root of the repository the program runs in, or the working directory outside one.
        public static string WorkingRoot()
        {
            string start = Directory.GetCurrentDirectory();
            return RepositoryRootAbove(start) ?? start;
        }

        public static string? RepositoryRootAbove(string start)
        {
            for (string? folder = start; folder != null; folder = Path.GetDirectoryName(folder))
            {
                string marker = Path.Combine(folder, ".git");
                // .git is a file in a worktree.
                if (Directory.Exists(marker) || File.Exists(marker))
                {
                    return folder;
                }
            }
            return null;
        }
    }
}
