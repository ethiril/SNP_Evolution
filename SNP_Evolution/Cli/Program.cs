using System;
using SnpEvolution.Cli;

namespace SnpEvolution.Cli
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length > 0)
            {
                return CommandLine.Run(args);
            }
            // The menus draw with box and arrow characters.
            Console.OutputEncoding = System.Text.Encoding.UTF8;
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
