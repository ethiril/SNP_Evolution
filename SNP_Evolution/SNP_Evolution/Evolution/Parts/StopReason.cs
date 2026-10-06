using System.Text.RegularExpressions;

namespace SnpEvolution.Evolution.Parts
{
    // Why checking a part went no further.
    public enum Stop
    {
        EveryInputChecked,
        BoundReached,
        TimeLimit,
        TooWide,
        Counterexample,
        NoSpecification,
        NotAComposition,
        NotThroughPorts,
        DoesNotFit,

        // Anything else a part file says, kept as written.
        Other,
    }

    // Detail is what the stop was about: the input of a counterexample or of a case too wide to follow, the seconds of a
    // time limit, the bound reached, or a refusal's message. Part files and the verify command write the text ToString
    // gives, and Parse reads it back, so part files written before the reason was typed still load.
    public sealed partial record StopReason(Stop Stop, string Detail = "")
    {
        public override string ToString() => Stop switch
        {
            Stop.EveryInputChecked => "every input checked",
            Stop.BoundReached => $"bound {Detail} reached",
            Stop.TimeLimit => $"time limit of {Detail} s",
            Stop.TooWide => $"{Detail} has too many computations to follow exactly",
            Stop.Counterexample => $"counterexample at {Detail}",
            Stop.NoSpecification => "the contract has no specification",
            _ => Detail,
        };

        public static StopReason Parse(string text) => text switch
        {
            "every input checked" => new StopReason(Stop.EveryInputChecked),
            "the contract has no specification" => new StopReason(Stop.NoSpecification),
            _ when BoundReached().Match(text) is { Success: true } match => new StopReason(Stop.BoundReached, match.Groups[1].Value),
            _ when TimeLimit().Match(text) is { Success: true } match => new StopReason(Stop.TimeLimit, match.Groups[1].Value),
            _ when TooWide().Match(text) is { Success: true } match => new StopReason(Stop.TooWide, match.Groups[1].Value),
            _ when text.StartsWith("counterexample at ", System.StringComparison.Ordinal) => new StopReason(Stop.Counterexample, text["counterexample at ".Length..]),
            _ => new StopReason(Stop.Other, text),
        };

        [GeneratedRegex(@"^bound (\S+) reached$")]
        private static partial Regex BoundReached();

        [GeneratedRegex(@"^time limit of (\S+) s$")]
        private static partial Regex TimeLimit();

        [GeneratedRegex(@"^(.+) has too many computations to follow exactly$")]
        private static partial Regex TooWide();
    }
}
