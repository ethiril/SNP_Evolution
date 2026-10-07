using System;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // Non-interactive commands, so runs and benchmarks can be scripted. Each command declares its options in its own class
    // (see CommandRegistry); unknown options, missing values and values a command cannot take are refused with usage.
    internal static class CommandLine
    {
        public static int Run(string[] args) => (int)Execute(args);

        private static ExitCode Execute(string[] args)
        {
            if (CommandRegistry.Find(args[0]) is not Command command)
            {
                Console.Error.WriteLine($"There is no command '{args[0]}'.");
                Console.Error.WriteLine(CommandRegistry.Usage);
                return ExitCode.Usage;
            }
            if (CommandArgs.Parse(command, args, out string error) is not CommandArgs parsed)
            {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine("Usage: " + command.Usage);
                foreach (Option option in command.Options)
                {
                    Console.Error.WriteLine($"  {option.Usage}: {option.Help}");
                }
                return ExitCode.Usage;
            }
            return command.Run(parsed);
        }
    }
}
