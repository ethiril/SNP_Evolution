using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Search;
using SnpEvolution.Search.Benchmarking;

namespace SnpEvolution.Cli
{
    internal sealed class TasksCommand : Command
    {
        public override string Name => "tasks";

        public override string Summary => "Lists the suite tasks.";

        public override IReadOnlyList<Option> Options => Array.Empty<Option>();

        public override ExitCode Run(CommandArgs args)
        {
            TaskSuite.All.ToList().ForEach(task => Console.WriteLine(task.Name));
            return ExitCode.Success;
        }
    }

    internal sealed class AlgorithmsCommand : Command
    {
        public override string Name => "algorithms";

        public override string Summary => "Lists the searches.";

        public override IReadOnlyList<Option> Options => Array.Empty<Option>();

        public override ExitCode Run(CommandArgs args)
        {
            SearchCatalog.All.ToList().ForEach(search => Console.WriteLine(search.Name));
            return ExitCode.Success;
        }
    }
}
