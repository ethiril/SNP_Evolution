using System;
using System.IO;

namespace SnpEvolution.Cli
{
    internal sealed partial class MainMenu
    {
        // Builds a network that is correct by construction for a sequence or set target, then evolves it smaller
        // (see CompileSession). A set needs a register program, which is evolved unless the user gives a file.
        private void CompileAndShrink()
        {
            const int DefaultShrinkGenerations = 300;
            const int ProgramGenerations = 2000;
            if (!TargetMenu.EditTarget(settings))
            {
                return;
            }
            if (settings.Target.Kind == TargetKind.BinaryWord)
            {
                Console.Clear();
                ConsoleUi.PrintHeader();
                Console.WriteLine(" Only sequence and set targets can be compiled.");
                ConsoleUi.WaitForEnter(" Press enter to return to the menu.");
                return;
            }
            string? programFile = null;
            bool accepted = settings.Target.Kind == TargetKind.Sequence;
            if (!accepted)
            {
                ConsoleUi.PromptUntilAccepted("Register program file, or leave it empty to evolve one", "That file does not exist.", input =>
                {
                    programFile = input.Trim().Length > 0 ? input.Trim() : null;
                    return accepted = programFile == null || File.Exists(programFile);
                }, $"Target: {settings.Target.Kind} {settings.Target}", "One instruction per line, such as: 0: ADD r1 -> 1 | 2, 1: SUB r1 -> 1 else 2, 2: HALT",
                    "Register 0 is the output and can only be added to.");
            }
            if (!accepted)
            {
                return;
            }
            int shrinkGenerations = DefaultShrinkGenerations;
            accepted = false;
            ConsoleUi.PromptUntilAccepted("Generations to shrink the compiled network for", "Number was not a whole number of 0 or more.", input =>
            {
                if (input.Trim().Length == 0)
                {
                    return accepted = true;
                }
                return accepted = InputParsing.TryNonNegativeInt(input.Trim(), out shrinkGenerations);
            }, $"Target: {settings.Target.Kind} {settings.Target}", $"Leave it empty for {DefaultShrinkGenerations}, or 0 to only compile.");
            if (!accepted)
            {
                return;
            }
            Console.Clear();
            ConsoleUi.PrintHeader();
            Console.WriteLine(" Compiling {0} {1}, then shrinking it for {2} generations.", settings.Target.Kind, settings.Target, shrinkGenerations);
            Console.WriteLine(settings.Target.Kind == TargetKind.Sequence
                ? " The gaps are fitted with a recurrence, which is compiled into a network that makes them exactly."
                : programFile != null
                    ? $" The program in {programFile} is compiled with the standard ADD and SUB modules."
                    : $" A register program is evolved for up to {ProgramGenerations} generations, then compiled with the standard ADD and SUB modules.");
            Console.WriteLine();
            if (!ConsoleUi.WaitForEnterOrEscape(" Press enter to start, or ESC to go back."))
            {
                return;
            }
            CompileSession.Run(settings, new CompileSession.Options(shrinkGenerations, ProgramGenerations, programFile), random, Console.WriteLine);
            ConsoleUi.WaitForEnter("Press enter to continue.");
        }
    }
}
