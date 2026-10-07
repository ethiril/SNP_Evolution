namespace SnpEvolution.Tests.Fixtures
{
    // Points the process-wide console at strings until disposed, so a command's output stays out of the test log. Every
    // test running at the time shares the console, so a test using this belongs to ProcessStateCollection.
    internal sealed class ConsoleCapture : IDisposable
    {
        private readonly TextWriter output = Console.Out;
        private readonly TextWriter error = Console.Error;
        private readonly StringWriter printed = new StringWriter { NewLine = "\n" };
        private readonly StringWriter errors = new StringWriter { NewLine = "\n" };

        public ConsoleCapture()
        {
            Console.SetOut(printed);
            Console.SetError(errors);
        }

        public string Printed => printed.ToString();

        public string Errors => errors.ToString();

        public void Dispose()
        {
            Console.SetOut(output);
            Console.SetError(error);
        }
    }

    // Tests that swap the console, the working directory or the culture, which the whole process shares, run alone.
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class ProcessStateCollection
    {
        public const string Name = "Process-wide state";
    }
}
