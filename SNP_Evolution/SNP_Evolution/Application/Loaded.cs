using System;
using System.IO;

namespace SnpEvolution.Application
{
    // A value read from files, or why it could not be read, so callers report the reason rather than catch it.
    internal sealed record Loaded<T>(T? Value, string? Error) where T : class
    {
        public static Loaded<T> Of(T value) => new Loaded<T>(value, null);

        public static Loaded<T> Failed(string error) => new Loaded<T>(null, error);

        // The value read, or the reason when reading throws one of the errors a bad or missing file gives.
        public static Loaded<T> Try(Func<T> read)
        {
            try
            {
                return Of(read());
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                return Failed(exception.Message);
            }
        }

        public Loaded<TOut> Select<TOut>(Func<T, TOut> map) where TOut : class =>
            Value != null ? Loaded<TOut>.Of(map(Value)) : Loaded<TOut>.Failed(Error!);
    }
}
