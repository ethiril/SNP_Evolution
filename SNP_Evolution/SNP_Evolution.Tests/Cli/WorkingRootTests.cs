using SnpEvolution.Cli;

namespace SnpEvolution.Tests.Cli
{
    public class WorkingRootTests
    {
        [Fact]
        public void AGitFolderOrAWorktreesGitFileMarksTheRoot()
        {
            string folder = Path.Combine(Path.GetTempPath(), "snp-root-" + Guid.NewGuid().ToString("N"));
            string inside = Path.Combine(folder, "repository", "worktree", "deep");
            Directory.CreateDirectory(inside);
            try
            {
                Directory.CreateDirectory(Path.Combine(folder, "repository", ".git"));
                Assert.Equal(Path.Combine(folder, "repository"), Settings.RepositoryRootAbove(inside));

                File.WriteAllText(Path.Combine(folder, "repository", "worktree", ".git"), "gitdir: elsewhere");
                Assert.Equal(Path.Combine(folder, "repository", "worktree"), Settings.RepositoryRootAbove(inside));
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
