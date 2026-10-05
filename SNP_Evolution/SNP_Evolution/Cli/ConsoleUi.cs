using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SnpEvolution.Cli
{
    internal static class ConsoleUi
    {
        private const string Indent = "    ";

        // Returns null when the user presses ESC.
        public static int? Choose(Settings settings, string message, IReadOnlyList<string> options)
        {
            int boxWidth = options.Max(option => option.Length) + 4;
            int selection = 0;
            Console.CursorVisible = false;
            try
            {
                while (true)
                {
                    Console.Clear();
                    PrintSplash();
                    PrintConfiguration(settings);
                    Console.WriteLine("  " + message);
                    PrintOptions(options, selection, boxWidth);
                    switch (Console.ReadKey(true).Key)
                    {
                        case ConsoleKey.UpArrow:
                            selection = Math.Max(selection - 1, 0);
                            break;
                        case ConsoleKey.DownArrow:
                            selection = Math.Min(selection + 1, options.Count - 1);
                            break;
                        case ConsoleKey.Escape:
                            return null;
                        case ConsoleKey.Enter:
                            return selection;
                    }
                }
            }
            finally
            {
                Console.CursorVisible = true;
            }
        }

        // Re-prompts until tryApply accepts the input, or the user presses ESC.
        public static void PromptUntilAccepted(string request, string invalidMessage, Func<string, bool> tryApply)
        {
            while (true)
            {
                Console.Clear();
                PrintSplash();
                Console.WriteLine(request + ", or press ESC to return to the last menu: ");
                string? input = ReadLineWithCancel();
                if (input == null || tryApply(input))
                {
                    return;
                }
                Console.WriteLine();
                WaitForEnter(invalidMessage + " Press enter to try again.");
            }
        }

        // Returns null when the user presses ESC.
        public static string? ReadLineWithCancel()
        {
            var buffer = new StringBuilder();
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        return buffer.ToString();
                    case ConsoleKey.Escape:
                        return null;
                    case ConsoleKey.Backspace when buffer.Length > 0:
                        buffer.Length--;
                        Console.Write("\b \b");
                        break;
                    default:
                        if (!char.IsControl(key.KeyChar))
                        {
                            buffer.Append(key.KeyChar);
                            Console.Write(key.KeyChar);
                        }
                        break;
                }
            }
        }

        public static void WaitForEnter(string message)
        {
            Console.WriteLine(message);
            Console.ReadLine();
        }

        public static void PrintSplash()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  _____ _   _  ______   _____           _                     ");
            Console.WriteLine(" /  ___| \\ | | | ___ \\ /  ___|         | |                    ");
            Console.WriteLine(" \\ `--.|  \\| | | |_/ / \\ `--. _   _ ___| |_ ___ _ __ ___  ___ ");
            Console.WriteLine("  `--. \\ . ` | |  __/   `--. \\ | | / __| __/ _ \\ '_ ` _ \\/ __|");
            Console.WriteLine(" /\\__/ / |\\  | | |     /\\__/ / |_| \\__ \\ ||  __/ | | | | \\__ \\");
            Console.WriteLine(" \\____/\\_| \\_/ \\_|     \\____/ \\__, |___/\\__\\___|_| |_| |_|___/");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("  _____         _           _");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("  ._/ |\n");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(" |   __|_ _ ___| |_ _ ___ _| |");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(".|___/ \n");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" |   __| | | . | | | | -_| . |");
            Console.WriteLine(" |_____|\\_/|___|_|\\_/|___|___|\n\n");
            Console.ResetColor();
            Console.WriteLine(" Please select an option from the menu below using your arrow and enter keys:\n");
        }

        public static void WriteColoured(ConsoleColor colour, object value)
        {
            Console.ForegroundColor = colour;
            Console.Write(value);
            Console.ResetColor();
        }

        private static void PrintConfiguration(Settings settings)
        {
            Console.WriteLine(" Current Configuration:");
            Console.Write(" Enabled Experimental Rules : ");
            WriteColoured(settings.ExperimentalRules ? ConsoleColor.Green : ConsoleColor.Red, settings.ExperimentalRules);
            Console.Write("; Max Steps per network: ");
            WriteColoured(ConsoleColor.Cyan, settings.MaxSteps);
            Console.Write("; Step-Through amount per network: ");
            WriteColoured(ConsoleColor.Cyan, settings.Repetitions);
            Console.Write(";\n Genetic Algorithm Population Size: ");
            WriteColoured(ConsoleColor.Cyan, settings.PopulationSize);
            Console.Write("; Mutation Rate: ");
            WriteColoured(ConsoleColor.Cyan, settings.MutationRate);
            Console.Write("; Maximum Number of Generations: ");
            WriteColoured(ConsoleColor.Cyan, settings.MaxGenerations);
            Console.Write(";\n Engine: ");
            WriteColoured(ConsoleColor.Cyan, settings.Engine.Name);
            Console.Write("; Fitness Function: ");
            WriteColoured(ConsoleColor.Cyan, settings.FitnessFunction.Name);
            Console.Write("; Genetic Algorithm: ");
            WriteColoured(ConsoleColor.Cyan, settings.Algorithm.Name);
            Console.Write(";\n Expected Set: {" + string.Join("\t", settings.ExpectedSet) + "}\n\n");
        }

        private static void PrintOptions(IReadOnlyList<string> options, int selection, int boxWidth)
        {
            Console.WriteLine(Indent + new string('-', boxWidth));
            for (int index = 0; index < options.Count; index++)
            {
                Console.Write(Indent);
                if (index == selection)
                {
                    Console.BackgroundColor = ConsoleColor.DarkCyan;
                }
                Console.Write("| " + options[index].PadRight(boxWidth - 4) + " |");
                Console.ResetColor();
                Console.WriteLine();
            }
            Console.WriteLine(Indent + new string('-', boxWidth));
        }
    }
}
