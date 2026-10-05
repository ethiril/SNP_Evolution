using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SnpEvolution.Cli
{
    internal static class ConsoleUi
    {
        private const string Indent = "    ";
        private const string MenuKeys = " [Up/Down] move   [Enter] select   [1-9] pick   [Esc] back";

        // Returns null when the user goes back with ESC, Backspace or the left arrow. Options are numbered, and a
        // number key picks its option at once; a letter key moves to the next option starting with it.
        public static int? Choose(Settings settings, string title, IReadOnlyList<string> options, int initial = 0, bool splash = false)
        {
            int boxWidth = options.Max(option => option.Length) + 8;
            int selection = Math.Clamp(initial, 0, options.Count - 1);
            Console.CursorVisible = false;
            try
            {
                while (true)
                {
                    Console.Clear();
                    if (splash)
                    {
                        PrintSplash();
                    }
                    else
                    {
                        PrintHeader();
                    }
                    PrintStatus(settings);
                    if (title.Length > 0)
                    {
                        WriteLineColoured(ConsoleColor.Yellow, " " + title);
                    }
                    PrintOptions(options, selection, boxWidth);
                    WriteLineColoured(ConsoleColor.DarkGray, MenuKeys);
                    ConsoleKeyInfo key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.UpArrow:
                            selection = (selection + options.Count - 1) % options.Count;
                            break;
                        case ConsoleKey.DownArrow:
                            selection = (selection + 1) % options.Count;
                            break;
                        case ConsoleKey.Home:
                            selection = 0;
                            break;
                        case ConsoleKey.End:
                            selection = options.Count - 1;
                            break;
                        case ConsoleKey.Escape:
                        case ConsoleKey.Backspace:
                        case ConsoleKey.LeftArrow:
                            return null;
                        case ConsoleKey.Enter:
                        case ConsoleKey.RightArrow:
                            return selection;
                        default:
                            int picked = key.KeyChar - '1';
                            if (picked >= 0 && picked < Math.Min(9, options.Count))
                            {
                                return picked;
                            }
                            if (char.IsLetter(key.KeyChar))
                            {
                                selection = NextStartingWith(options, selection, key.KeyChar);
                            }
                            break;
                    }
                }
            }
            finally
            {
                Console.CursorVisible = true;
            }
        }

        public static bool Confirm(Settings settings, string question) => Choose(settings, question, new[] { "Yes", "No" }) == 0;

        // A menu row with its current value lined up after it.
        public static string Row(string label, object value) => label.PadRight(28, ' ') + value;

        // Re-prompts until tryApply accepts the input, or the user presses ESC. The notes show above the prompt.
        public static void PromptUntilAccepted(string request, string invalidMessage, Func<string, bool> tryApply, params string[] notes)
        {
            while (true)
            {
                Console.Clear();
                PrintHeader();
                foreach (string note in notes)
                {
                    WriteLineColoured(ConsoleColor.DarkGray, " " + note);
                }
                if (notes.Length > 0)
                {
                    Console.WriteLine();
                }
                Console.WriteLine(" " + request + " (ESC to go back):");
                Console.Write(" > ");
                string? input = ReadLineWithCancel();
                if (input == null || tryApply(input))
                {
                    return;
                }
                Console.WriteLine();
                WriteLineColoured(ConsoleColor.Red, " " + invalidMessage);
                WaitForEnter(" Press enter to try again.");
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

        // True on Enter, false on ESC.
        public static bool WaitForEnterOrEscape(string message)
        {
            Console.WriteLine(message);
            while (true)
            {
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.Enter:
                        return true;
                    case ConsoleKey.Escape:
                        return false;
                }
            }
        }

        public static void PrintHeader()
        {
            WriteLineColoured(ConsoleColor.Cyan, " SN P Systems Evolved");
            Console.WriteLine();
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
            Console.WriteLine(" |_____|\\_/|___|_|\\_/|___|___|\n");
            Console.ResetColor();
        }

        public static void WriteColoured(ConsoleColor colour, object value)
        {
            Console.ForegroundColor = colour;
            Console.Write(value);
            Console.ResetColor();
        }

        public static void WriteLineColoured(ConsoleColor colour, object value)
        {
            WriteColoured(colour, value);
            Console.WriteLine();
        }

        // What an evolution run would do with the current settings, in three lines.
        private static void PrintStatus(Settings settings)
        {
            string task = settings.Task == Catalog.TargetTask ? $"{settings.Target.Kind} target {settings.Target}" : settings.Task.Name;
            StatusLine("Task", task);
            StatusLine("Search", $"{settings.Algorithm.Name} | population {settings.PopulationSize} | {settings.MaxGenerations} generations | " +
                $"mutation {settings.MutationRate} | up to {settings.MaxNeurons} neurons | experimental rules {(settings.ExperimentalRules ? "on" : "off")}");
            StatusLine("Simulation", $"{settings.Engine.Name} | {settings.MaxSteps} steps x {settings.Repetitions} runs | " +
                $"{settings.RuleForm} rules | {settings.OutputTiming} timing");
            Console.WriteLine();
        }

        private static void StatusLine(string label, string value)
        {
            WriteColoured(ConsoleColor.DarkGray, " " + label.PadRight(11));
            WriteLineColoured(ConsoleColor.Cyan, value);
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
                string number = index < 9 ? $"{index + 1}." : "  ";
                Console.Write("| " + $"{number,-3} {options[index]}".PadRight(boxWidth - 4) + " |");
                Console.ResetColor();
                Console.WriteLine();
            }
            Console.WriteLine(Indent + new string('-', boxWidth));
        }

        private static int NextStartingWith(IReadOnlyList<string> options, int selection, char letter)
        {
            for (int offset = 1; offset <= options.Count; offset++)
            {
                int index = (selection + offset) % options.Count;
                if (options[index].StartsWith(letter.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
            return selection;
        }
    }
}
