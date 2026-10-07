using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;
using SnpEvolution.Evolution.Verification;

namespace SnpEvolution.Cli
{
    internal sealed class VerifyCommand : Command
    {
        private static readonly Option<long> Seconds = new Option<long>("seconds", ValueKinds.PositiveLong, "time to spend on each part; 60 unless given");

        private static readonly Option<int> Bound = new Option<int>("bound", ValueKinds.PositiveInt, "the highest bound to prove up to");

        public override string Name => "verify";

        public override string Summary => "Proves each part's contract for every input up to a bound, raised until the time per part runs out, and records it in the part's file.";

        public override IReadOnlyList<Option> Options { get; } =
            new Option[] { CommonOptions.Part, SettingOptions.Library.Option, CommonOptions.Only, Seconds, Bound };

        public override ExitCode Run(CommandArgs args)
        {
            var settings = new Settings();
            SettingOptions.Library.ApplyFrom(args, settings);
            Loaded<IReadOnlyList<PartFile>> loaded = args.Find(CommonOptions.Part) is string file
                ? PartLibraries.ReadPart(file).Select<IReadOnlyList<PartFile>>(part => new[] { part })
                : PartLibraries.PartsIn(settings.PartLibraryFolder);
            if (loaded.Value is not IReadOnlyList<PartFile> parts)
            {
                return Refuse(loaded.Error!);
            }
            if (args.Find(CommonOptions.Only) is string[] names)
            {
                parts = parts.Where(each => CommonOptions.OnlyMatches(names, each.Part.Contract.Name)).ToList();
            }
            if (parts.Count == 0)
            {
                return Refuse("There are no parts to verify.");
            }
            var limits = new ProofLimits(TimeSpan.FromSeconds(args.Get(Seconds, 60)), args.Get(Bound, int.MaxValue));
            IReadOnlyList<VerifiedPart> verified = VerifyService.Run(parts, limits, part =>
            {
                Console.WriteLine($"{part.Contract}: {part.Proven} in {part.Elapsed.TotalSeconds:0.0} s.");
                if (part.Counterexample != null)
                {
                    Console.WriteLine(part.Counterexample);
                }
            });
            return verified.Any(part => part.Counterexample != null) ? ExitCode.Refuted : ExitCode.Success;
        }
    }
}
