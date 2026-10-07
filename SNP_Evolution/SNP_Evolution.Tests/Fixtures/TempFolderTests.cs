namespace SnpEvolution.Tests.Fixtures
{
    public class TempFolderTests
    {
        [Fact]
        public void DisposingDeletesTheFolderAndEverythingInIt()
        {
            var temp = new TempFolder("snp-temp-test").Made();
            Directory.CreateDirectory(Path.Combine(temp.Path, "inner"));
            File.WriteAllText(Path.Combine(temp.Path, "inner", "file.txt"), "kept until disposed");

            temp.Dispose();

            Assert.False(Directory.Exists(temp.Path));
        }
    }
}
