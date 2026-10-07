namespace SnpEvolution.Tests.Fixtures
{
    [Collection(ProcessStateCollection.Name)]
    public class ConsoleCaptureTests
    {
        [Fact]
        public void CapturesBothStreamsAndPutsTheConsoleBack()
        {
            TextWriter output = Console.Out, error = Console.Error;

            using (var console = new ConsoleCapture())
            {
                Console.WriteLine("printed");
                Console.Error.WriteLine("failed");

                Assert.Equal("printed\n", console.Printed);
                Assert.Equal("failed\n", console.Errors);
            }

            Assert.Same(output, Console.Out);
            Assert.Same(error, Console.Error);
        }
    }
}
