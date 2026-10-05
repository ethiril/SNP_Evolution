using System;
using SnpEvolution.Cli;

namespace SnpEvolution
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length > 0)
            {
                return CommandLine.Run(args);
            }
            if (OperatingSystem.IsWindows())
            {
                Console.SetWindowSize(Console.WindowWidth, Math.Min(Console.WindowHeight + 5, Console.LargestWindowHeight));
            }
            new MainMenu().Run();
            Console.WriteLine("Thanks for testing! :)");
            return 0;
        }
    }
}
