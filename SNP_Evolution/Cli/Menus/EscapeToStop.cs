using System;
using System.Threading;
using SnpEvolution.Specs.Accounting;

namespace SnpEvolution.Cli
{
    // While a command runs from the menu, Esc asks it to stop early (see EarlyStop): searches finish the generation
    // they are on and keep their best so far, and the command reports what it has. Commands read no keys while they
    // run, so watching the keyboard takes none they need.
    internal sealed class EscapeToStop : IDisposable
    {
        private const int PollMilliseconds = 50;

        private readonly EarlyStop stop = EarlyStop.Begin();
        private readonly Thread watcher;
        private volatile bool finished;

        public EscapeToStop()
        {
            watcher = new Thread(Watch) { IsBackground = true, Name = "Esc to stop" };
            watcher.Start();
        }

        public bool Stopped => stop.IsRequested;

        public void Dispose()
        {
            finished = true;
            watcher.Join();
            stop.Dispose();
        }

        private void Watch()
        {
            while (!finished)
            {
                if (!KeyWaiting())
                {
                    Thread.Sleep(PollMilliseconds);
                    continue;
                }
                if (Console.ReadKey(intercept: true).Key == ConsoleKey.Escape && !stop.IsRequested)
                {
                    stop.Request();
                    Console.WriteLine(" Stopping early . . .");
                }
            }
        }

        // A console without a keyboard, such as one whose input is redirected, has nothing to watch.
        private static bool KeyWaiting()
        {
            try
            {
                return Console.KeyAvailable;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
