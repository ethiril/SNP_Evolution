using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // Lists the library's parts, or shows the ones asked for in full.
    internal sealed class PartsCommand : Command
    {
        public override string Name => "parts";

        public override string Summary => "Lists the parts kept in the library, or shows each part asked for in full: contract, ports, cost, proof, origin, what it reads on each case and its network.";

        public override IReadOnlyList<Option> Options { get; } = new Option[] { SettingOptions.Library.Option, CommonOptions.Only, CommonOptions.Part };

        public override ExitCode Run(CommandArgs args)
        {
            Settings settings = args.StartingSettings();
            SettingOptions.Library.ApplyFrom(args, settings);
            if (args.Find(CommonOptions.Part) is string file)
            {
                Loaded<PartFile> read = PartLibraries.ReadPart(file);
                if (read.Value is not PartFile one)
                {
                    return Refuse(read.Error!);
                }
                Console.Write(LibraryView.Describe(one));
                return ExitCode.Success;
            }
            Loaded<IReadOnlyList<PartFile>> loaded = PartLibraries.PartsIn(settings.PartLibraryFolder);
            if (loaded.Value is not IReadOnlyList<PartFile> parts)
            {
                return Refuse(loaded.Error!);
            }
            if (args.Find(CommonOptions.Only) is not string[] names)
            {
                Console.Write(LibraryView.Table(parts));
                return ExitCode.Success;
            }
            List<PartFile> matching = parts.Where(each => CommonOptions.OnlyMatches(names, each.Part.Contract.Name)).ToList();
            if (matching.Count == 0)
            {
                return Refuse($"No part in {settings.PartLibraryFolder} matches. The library has: {string.Join(", ", parts.Select(each => each.Part.Contract.Name))}.");
            }
            Console.Write(string.Join(Environment.NewLine, matching.Select(LibraryView.Describe)));
            return ExitCode.Success;
        }
    }
}
