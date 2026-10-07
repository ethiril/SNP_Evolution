using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SnpEvolution.Application;
using SnpEvolution.Model;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Storage;

namespace SnpEvolution.Cli
{
    // Runs one network with the simulation settings: a network with inputs is scored on the task, one without shows
    // the numbers it generated and one run's spike train.
    internal sealed class RunCommand : Command
    {
        internal static readonly Option<string> Network = new Option<string>("network", ValueKinds.Text,
            "a saved network file, or natural or even for the reference networks", "FILE|natural|even");

        public override string Name => "run";

        public override string Summary => "Runs a saved or reference network and prints the numbers it generated and one run's spike train, or its score on the task when it has inputs.";

        public override IReadOnlyList<Option> Options { get; } =
            new Option[] { Network, CommonOptions.Target, CommonOptions.Kind, CommonOptions.Task, CommonOptions.Seed }.Concat(SettingOptions.Flags(SettingOptions.Simulation)).ToList();

        public override IReadOnlyList<Option> Required { get; } = new[] { Network };

        public override ExitCode Run(CommandArgs args)
        {
            string source = args.Get(Network, "");
            Network? network = source.ToLowerInvariant() switch
            {
                "natural" => ReferenceNetworks.NaturalNumbers(),
                "even" => ReferenceNetworks.EvenNumbers(),
                _ => NetworkFiles.Load(source),
            };
            if (network == null)
            {
                return Refuse($"Could not load a network from {source}.");
            }
            if (TargetArgs.Settings(args, SettingOptions.Simulation, needsOne: false) is not Settings settings)
            {
                return ExitCode.Usage;
            }
            Console.Write(NetworkNotation.Format(network));
            Stopwatch stopwatch = Stopwatch.StartNew();
            NetworkRun run = NetworkRunService.Run(settings, network, RunSeed.For(CommonOptions.SeedFrom(args)));
            stopwatch.Stop();
            Console.WriteLine("Outputs over {0} runs of up to {1} steps on the {2} engine:", settings.Repetitions, settings.MaxSteps, settings.Engine.Name);
            Console.WriteLine(string.Join("\t", run.Outputs));
            switch (run)
            {
                case NetworkRun.Scored scored:
                    Console.WriteLine("On {0}: fitness {1}, {2}", scored.Task.Name, scored.Score.Fitness, scored.Score.Description);
                    break;
                case NetworkRun.SpikeTrain train:
                    Console.WriteLine("One run's spike train over {0} steps:", settings.MaxSteps);
                    Console.WriteLine(SpikeTrains.Format(SpikeTrains.Word(train.SpikeSteps, settings.MaxSteps)));
                    Console.WriteLine("Intervals between its spikes: {0}", string.Join(",", SpikeTrains.Intervals(train.SpikeSteps)));
                    break;
            }
            Console.WriteLine($"Ran in {stopwatch.Elapsed.TotalSeconds:0.00} s.");
            return ExitCode.Success;
        }
    }
}
