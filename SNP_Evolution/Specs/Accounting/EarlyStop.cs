using System;
using System.Threading;

namespace SnpEvolution.Specs.Accounting
{
    // Lets whoever runs a command end it early, as the menu does on Esc. Begin opens a scope for one run: the code it
    // calls, and the tasks and parallel loops that code starts, see the request, while runs outside the scope, such as
    // tests running beside it, never do. A search looks before each generation, as it looks at its budget, and stops as
    // cancelled, keeping what it has; a command running several searches or proofs starts no more.
    public sealed class EarlyStop : IDisposable
    {
        private static readonly AsyncLocal<EarlyStop?> Current = new AsyncLocal<EarlyStop?>();

        private readonly EarlyStop? outer;
        private volatile bool requested;

        private EarlyStop(EarlyStop? outer)
        {
            this.outer = outer;
        }

        public static bool Requested => Current.Value?.requested ?? false;

        public static EarlyStop Begin()
        {
            var stop = new EarlyStop(Current.Value);
            Current.Value = stop;
            return stop;
        }

        public bool IsRequested => requested;

        // Safe from any thread, such as one watching the keyboard.
        public void Request() => requested = true;

        public void Dispose() => Current.Value = outer;
    }
}
