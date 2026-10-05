using System;
using SnpEvolution.Cli;

namespace SnpEvolution
{
    internal static class Program
    {
        private static void Main()
        {
            if (OperatingSystem.IsWindows())
            {
                Console.SetWindowSize(Console.WindowWidth, Math.Min(Console.WindowHeight + 5, Console.LargestWindowHeight));
            }
            new MainMenu().Run();
            Console.WriteLine("Thanks for testing! :)");
        }
    }
}
