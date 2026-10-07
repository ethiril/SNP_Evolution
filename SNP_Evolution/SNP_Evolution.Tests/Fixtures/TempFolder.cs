namespace SnpEvolution.Tests.Fixtures
{
    // A folder of its own under the system's temporary folder, named but not made, and deleted with everything in it
    // when disposed.
    internal sealed class TempFolder : IDisposable
    {
        public TempFolder(string name = "snp")
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}");
        }

        public string Path { get; }

        public TempFolder Made()
        {
            Directory.CreateDirectory(Path);
            return this;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
