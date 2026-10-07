using System;
using System.Collections.Generic;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // --generations and --lexicase are for evolving a register program, which a set target needs unless --program gives one.
    internal sealed class CompileCommand : Command
    {
        private static readonly Option<string> Program = new Option<string>("program", ValueKinds.Text, "a register program to compile, instead of evolving one", "FILE");

        private static readonly Option<int> Generations = new Option<int>("generations", ValueKinds.PositiveInt, $"generations to evolve a register program for; {CompileService.DefaultProgramGenerations} unless given");

        private static readonly Option<bool> Lexicase = new Option<bool>("lexicase", ValueKinds.Switch, "evolve the register program with lexicase parents; on unless given");

        private static readonly Option<int> Shrink = new Option<int>("shrink", ValueKinds.NonNegativeInt, $"generations to shrink for, or 0 to only compile; {CompileService.DefaultShrinkGenerations} unless given");

        public override string Name => "compile";

        public override string Summary => "Compiles a sequence target's recurrence or a set target's register program into a network that is correct by construction, then shrinks it.";

        public override IReadOnlyList<Option> Options { get; } = new Option[]
        {
            CommonOptions.Target, CommonOptions.Kind, Program, Generations, Lexicase, Shrink, CommonOptions.Seed, SettingOptions.Population.Option,
        };

        public override IReadOnlyList<Option> Required { get; } = new[] { CommonOptions.Target };

        public override ExitCode Run(CommandArgs args)
        {
            if (TargetArgs.Settings(args, new[] { SettingOptions.Population }) is not Settings settings)
            {
                return ExitCode.Usage;
            }
            var request = new CompileRequest(args.Get(Shrink, CompileService.DefaultShrinkGenerations), args.Find(Program),
                args.Get(Generations, CompileService.DefaultProgramGenerations), args.Get(Lexicase, true));
            CompileResult result = CompileService.Run(settings, request, RunSeed.For(CommonOptions.SeedFrom(args)), Console.WriteLine);
            return result.Compiled == null ? ExitCode.Usage : result.Solved ? ExitCode.Success : ExitCode.Unsolved;
        }
    }
}
