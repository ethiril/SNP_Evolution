using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;
using SnpEvolution.Evolution.Contracts;

namespace SnpEvolution.Cli
{
    internal sealed class EvolvePartsCommand : Command
    {
        private static readonly Option<long> Budget = new Option<long>("budget", ValueKinds.PositiveLong, "evaluations to search for each part");

        private static readonly Option<bool> Redo = new Option<bool>("redo", ValueKinds.Switch, "evolve parts the library already has again");

        private static readonly Option<int> Robust = new Option<int>("robust", ValueKinds.NonNegativeInt, "shrink towards the most robust part at jitter J rather than the smallest", "J");

        public override string Name => "evolve-parts";

        public override string Summary => "Evolves, verifies, shrinks and saves a part for each first-part contract the library has no part for, and reports robustness to jitter.";

        public override IReadOnlyList<Option> Options { get; } = new Option[]
        {
            CommonOptions.Seed, Budget, CommonOptions.Only, SettingOptions.Library.Option, CommonOptions.Sampled, CommonOptions.Configurations, Redo,
            SettingOptions.HardwareProfile.Option, Robust,
        };

        public override ExitCode Run(CommandArgs args)
        {
            List<Contract> contracts = FirstParts.Contracts.ToList();
            if (args.Find(CommonOptions.Only) is string[] names)
            {
                if (names.FirstOrDefault(name => !contracts.Any(contract => contract.Name.Contains(name, StringComparison.OrdinalIgnoreCase))) is string unknown)
                {
                    return Refuse($"No first-part contract matches '{unknown}'. The contracts are: {string.Join(", ", contracts.Select(contract => contract.Name))}.");
                }
                contracts = contracts.Where(contract => CommonOptions.OnlyMatches(names, contract.Name)).ToList();
            }
            var settings = new Settings();
            SettingOptions.Apply(settings, args, new SettingOption[] { SettingOptions.Library, SettingOptions.HardwareProfile });
            settings.PartBudget = args.Get(Budget, settings.PartBudget);
            var request = new PartsRequest(contracts, CommonOptions.SeedFrom(args) ?? RunSeed.Repeatable, args.Get(Redo, false), CommonOptions.EngineFrom(args), args.Get(Robust, 0));
            PartsResult result = PartsService.Run(settings, request, Console.WriteLine);
            return result.Error is string error ? Refuse(error) : result.AllSolved ? ExitCode.Success : ExitCode.Unsolved;
        }
    }
}
