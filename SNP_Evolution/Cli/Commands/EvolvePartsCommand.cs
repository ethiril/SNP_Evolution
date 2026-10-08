using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;
using SnpEvolution.Specs.Contracts;

namespace SnpEvolution.Cli
{
    internal sealed class EvolvePartsCommand : Command
    {
        private static readonly Option<bool> Redo = new Option<bool>("redo", ValueKinds.Switch, "evolve parts the library already has again");

        private static readonly Option<int> Robust = new Option<int>("robust", ValueKinds.NonNegativeInt, "shrink towards the most robust part at jitter J rather than the smallest", "J");

        private static readonly Option<bool> Staged = new Option<bool>("staged", ValueKinds.Switch, "evolve on the cases with the smallest inputs first, adding more as each stage is solved (on unless given)");

        private static readonly Option<PartRoute> Route = new Option<PartRoute>("route", ValueKinds.Enum<PartRoute>(),
            "find parts with count ports by search, by compiling a register program, or both (compile, then search if that fails)");

        public override string Name => "evolve-parts";

        public override string Summary => "Evolves, verifies, shrinks and saves a part for each first-part contract the library has no part for, and reports robustness to jitter.";

        public override IReadOnlyList<Option> Options { get; } = new Option[]
        {
            CommonOptions.Seed, SettingOptions.PartBudget.Option, CommonOptions.Only, SettingOptions.Library.Option, CommonOptions.Sampled, CommonOptions.Configurations, Redo,
            SettingOptions.HardwareProfile.Option, Robust, Staged, Route,
        };

        public override ExitCode Run(CommandArgs args)
        {
            List<Contract> contracts = FirstParts.Contracts.ToList();
            if (args.Find(CommonOptions.Only) is string[] names)
            {
                List<Contract> known = contracts.Concat(ArithmeticParts.BuildingBlocks).ToList();
                if (names.FirstOrDefault(name => !known.Any(contract => contract.Name.Contains(name, StringComparison.OrdinalIgnoreCase))) is string unknown)
                {
                    return Refuse($"No first-part or building-block contract matches '{unknown}'. The contracts are: {string.Join(", ", known.Select(contract => contract.Name))}.");
                }
                contracts = known.Where(contract => names.Any(name => Picks(name, contract, known))).ToList();
            }
            Settings settings = args.StartingSettings();
            SettingOptions.Apply(settings, args, new SettingOption[] { SettingOptions.Library, SettingOptions.HardwareProfile, SettingOptions.PartBudget });
            var request = new PartsRequest(contracts, CommonOptions.SeedFrom(args) ?? RunSeed.Repeatable, args.Get(Redo, false), CommonOptions.EngineFrom(args), args.Get(Robust, 0), args.Get(Staged, true), args.Get(Route, PartRoute.Both));
            PartsResult result = PartsService.Run(settings, request, Console.WriteLine);
            return result.Error is string error ? Refuse(error) : result.AllSolved ? ExitCode.Success : ExitCode.Unsolved;
        }

        // A name that is a contract's own picks only that one, so "add" need not also pick "add loop".
        private static bool Picks(string name, Contract contract, IReadOnlyList<Contract> known) =>
            known.Any(other => other.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ? contract.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                : contract.Name.Contains(name, StringComparison.OrdinalIgnoreCase);
    }
}
