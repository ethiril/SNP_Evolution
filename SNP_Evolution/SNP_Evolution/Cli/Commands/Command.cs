using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Cli
{
    // How a command ends, for scripts. Usage covers a bad command line and input the command cannot use.
    internal enum ExitCode
    {
        Success = 0,
        Usage = 1,
        Unsolved = 2,
        Refuted = 2,
        Differs = 3,
    }

    // A command declares its name, what it does and its options; parsing, usage and the README table come from these.
    // Adding a command means one subclass and one line in CommandRegistry.
    internal abstract class Command
    {
        public abstract string Name { get; }

        // One sentence for the usage text and the README table.
        public abstract string Summary { get; }

        public abstract IReadOnlyList<Option> Options { get; }

        // Options the command cannot run without; they are listed in Options as well.
        public virtual IReadOnlyList<Option> Required => Array.Empty<Option>();

        public abstract ExitCode Run(CommandArgs args);

        public string Usage => string.Join(" ", new[] { "snp-evolution", Name }.Concat(Options.Select(option => Required.Contains(option) ? option.Usage : $"[{option.Usage}]")));

        // Writes the reason to stderr and returns the usage exit code.
        protected static ExitCode Refuse(string reason)
        {
            Console.Error.WriteLine(reason);
            return ExitCode.Usage;
        }
    }
}
