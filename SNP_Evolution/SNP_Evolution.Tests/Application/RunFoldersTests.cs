using SnpEvolution.Application;

namespace SnpEvolution.Tests.Application
{
    public class RunFoldersTests
    {
        [Fact]
        public void AGitFolderOrAWorktreesGitFileMarksTheRoot()
        {
            using var temp = new TempFolder("snp-root");
            string folder = temp.Path;
            string inside = Path.Combine(folder, "repository", "worktree", "deep");
            Directory.CreateDirectory(inside);

            Directory.CreateDirectory(Path.Combine(folder, "repository", ".git"));
            Assert.Equal(Path.Combine(folder, "repository"), RunFolders.RepositoryRootAbove(inside));

            File.WriteAllText(Path.Combine(folder, "repository", "worktree", ".git"), "gitdir: elsewhere");
            Assert.Equal(Path.Combine(folder, "repository", "worktree"), RunFolders.RepositoryRootAbove(inside));
        }
    }
}
