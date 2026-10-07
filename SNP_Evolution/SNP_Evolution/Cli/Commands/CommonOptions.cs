using System;
using System.Linq;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // Options several commands take that are not settings of their own.
    internal static class CommonOptions
    {
        public static readonly Option<int> Seed = new Option<int>("seed", ValueKinds.NonNegativeInt, "makes the run repeatable");

        public static readonly Option<string> Target = new Option<string>("target", ValueKinds.Text, "positive numbers, or 0s and 1s with --kind binary", "VALUES");

        public static readonly Option<TargetKind> Kind = new Option<TargetKind>("kind",
            ValueKinds.Words(("set", TargetKind.Set), ("sequence", TargetKind.Sequence), ("binary", TargetKind.BinaryWord)), "the kind of target; sequence unless given");

        public static readonly Option<string> Task = new Option<string>("task", ValueKinds.Text, "suite tasks whose names contain NAME", "NAME");

        public static readonly Option<string> Algorithm = new Option<string>("algorithm", ValueKinds.Text, "searches whose names contain NAME", "NAME");

        public static readonly Option<bool> Sampled = new Option<bool>("engine", ValueKinds.Words(("exact", false), ("sampled", true)), "the exhaustive engine unless sampled");

        public static readonly Option<int> Configurations = new Option<int>("configurations", ValueKinds.PositiveInt, "caps the exhaustive engine's search width");

        public static readonly Option<string[]> Only = new Option<string[]>("only", ValueKinds.List, "only the contracts whose names contain one of these", "\"NAME,NAME\"");

        public static readonly Option<string> Part = new Option<string>("part", ValueKinds.Text, "a library part file", "FILE");

        public static readonly Option<string> Network = new Option<string>("network", ValueKinds.Text, "a saved network file", "FILE");

        public static readonly Option<string> Out = new Option<string>("out", ValueKinds.Text, "the folder to write to; export unless given", "DIR");

        public static readonly Option<bool> Check = new Option<bool>("check", ValueKinds.Switch, "check the file with the outside tool when it is installed; on unless given");

        public static readonly Option<int> Steps = new Option<int>("steps", ValueKinds.PositiveInt, "steps to run a network for; 50 unless given");

        // Whether the contract's name contains one of the names given to --only, ignoring case.
        public static bool OnlyMatches(string[] names, string contract) => names.Any(name => contract.Contains(name, StringComparison.OrdinalIgnoreCase));

        public static int? SeedFrom(CommandArgs args) => args.TryGet(Seed, out int seed) ? seed : null;

        public static EngineChoice EngineFrom(CommandArgs args) =>
            new EngineChoice(args.Get(Sampled, false), args.Get(Configurations, Simulation.ExhaustiveCpuEngine.DefaultMaxConfigurations));
    }
}
