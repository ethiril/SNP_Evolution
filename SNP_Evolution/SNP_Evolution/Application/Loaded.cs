namespace SnpEvolution.Application
{
    // A value read from files, or why it could not be read, so callers report the reason rather than catch it.
    internal sealed record Loaded<T>(T? Value, string? Error) where T : class
    {
        public static Loaded<T> Of(T value) => new Loaded<T>(value, null);

        public static Loaded<T> Failed(string error) => new Loaded<T>(null, error);
    }
}
