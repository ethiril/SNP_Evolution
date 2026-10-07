using System;
using System.Text;

namespace SnpEvolution.Cli
{
    // Typed input on its own screen, and waiting for a key; ConsoleUi draws the menus.
    internal static class ConsoleInput
    {
        // Re-prompts until tryApply accepts the input, or the user presses ESC. The notes show above the prompt.
        public static void PromptUntilAccepted(string request, string invalidMessage, Func<string, bool> tryApply, params string[] notes)
        {
            while (true)
            {
                Console.Clear();
                ConsoleUi.PrintHeader();
                foreach (string note in notes)
                {
                    ConsoleUi.WriteLineColoured(ConsoleColor.DarkGray, " " + note);
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
                ConsoleUi.WriteLineColoured(ConsoleColor.Red, " " + invalidMessage);
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
    }
}
