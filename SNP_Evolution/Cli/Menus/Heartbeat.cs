using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace SnpEvolution.Cli
{
    // Stands in for the console's output while a command runs from the menu, and says the command is still working
    // whenever nothing has been written for a while, so a long search that logs nothing looks busy rather than stuck.
    internal sealed class Heartbeat : TextWriter
    {
        public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(15);

        private readonly TextWriter inner;
        private readonly Func<TimeSpan> clock;
        private readonly TimeSpan quiet;
        private readonly bool coloured;
        private readonly object gate = new object();
        private readonly TimeSpan started;
        private TimeSpan lastWrite;
        private bool atLineStart = true;
        private Timer? timer;

        public Heartbeat(TextWriter inner, Func<TimeSpan> clock, TimeSpan quiet, bool coloured = false)
        {
            this.inner = inner;
            this.clock = clock;
            this.quiet = quiet;
            this.coloured = coloured;
            started = lastWrite = clock();
        }

        // Takes over the console's output until disposed.
        public static Heartbeat Start()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var heartbeat = new Heartbeat(Console.Out, () => stopwatch.Elapsed, Quiet, coloured: true);
            Console.SetOut(heartbeat);
            heartbeat.timer = new Timer(_ => heartbeat.Tick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            return heartbeat;
        }

        public override Encoding Encoding => inner.Encoding;

        // Writes the still-working line when the output has been quiet for long enough; the line itself counts as
        // output, so it repeats once per quiet spell.
        public void Tick()
        {
            lock (gate)
            {
                TimeSpan now = clock();
                if (now - lastWrite < quiet)
                {
                    return;
                }
                string line = $" Still working… {Elapsed(now - started)} so far.";
                if (!atLineStart)
                {
                    inner.WriteLine();
                }
                if (coloured)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                }
                inner.WriteLine(line);
                if (coloured)
                {
                    Console.ResetColor();
                }
                inner.Flush();
                lastWrite = now;
                atLineStart = true;
            }
        }

        public override void Write(char value)
        {
            lock (gate)
            {
                inner.Write(value);
                Wrote(value);
            }
        }

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }
            lock (gate)
            {
                inner.Write(value);
                Wrote(value[^1]);
            }
        }

        public override void Write(char[] buffer, int index, int count)
        {
            if (count == 0)
            {
                return;
            }
            lock (gate)
            {
                inner.Write(buffer, index, count);
                Wrote(buffer[index + count - 1]);
            }
        }

        public override void WriteLine(string? value)
        {
            lock (gate)
            {
                inner.WriteLine(value);
                Wrote('\n');
            }
        }

        public override void Flush()
        {
            lock (gate)
            {
                inner.Flush();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (timer != null)
                {
                    timer.Dispose();
                    Console.SetOut(inner);
                }
            }
            base.Dispose(disposing);
        }

        private void Wrote(char last)
        {
            lastWrite = clock();
            atLineStart = last == '\n';
        }

        private static string Elapsed(TimeSpan span) =>
            span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m" : span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes}m {span.Seconds}s" : $"{span.Seconds}s";
    }
}
