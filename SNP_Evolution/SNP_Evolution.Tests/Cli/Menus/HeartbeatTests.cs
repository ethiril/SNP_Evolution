using SnpEvolution.Cli;

namespace SnpEvolution.Tests.Cli.Menus
{
    // A command run from the menu says it is still working once its output has been quiet for a while, and only then.
    public class HeartbeatTests
    {
        private TimeSpan now;

        private (Heartbeat Heartbeat, StringWriter Output) Watch()
        {
            var output = new StringWriter { NewLine = "\n" };
            return (new Heartbeat(output, () => now, TimeSpan.FromSeconds(15)), output);
        }

        [Fact]
        public void SaysNothingWhileOutputKeepsComing()
        {
            (Heartbeat heartbeat, StringWriter output) = Watch();

            now = TimeSpan.FromSeconds(10);
            heartbeat.WriteLine("Evolving a part for fan-out.");
            now = TimeSpan.FromSeconds(20);
            heartbeat.Tick();

            Assert.Equal("Evolving a part for fan-out.\n", output.ToString());
        }

        [Fact]
        public void SaysStillWorkingOncePerQuietSpell()
        {
            (Heartbeat heartbeat, StringWriter output) = Watch();

            heartbeat.WriteLine("Evolving a part for fan-out.");
            now = TimeSpan.FromSeconds(15);
            heartbeat.Tick();
            now = TimeSpan.FromSeconds(20);
            heartbeat.Tick();
            now = TimeSpan.FromSeconds(95);
            heartbeat.Tick();

            Assert.Equal("Evolving a part for fan-out.\n Still working… 15s so far.\n Still working… 1m 35s so far.\n", output.ToString());
        }

        [Fact]
        public void StartsItsOwnLineAfterAnUnfinishedOne()
        {
            (Heartbeat heartbeat, StringWriter output) = Watch();

            heartbeat.Write("Checking");
            now = TimeSpan.FromSeconds(15);
            heartbeat.Tick();

            Assert.Equal("Checking\n Still working… 15s so far.\n", output.ToString());
        }
    }
}
