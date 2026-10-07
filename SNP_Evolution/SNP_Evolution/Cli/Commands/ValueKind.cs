using System;
using System.Linq;

namespace SnpEvolution.Cli
{
    // What an option or a menu prompt accepts. Expected is how the command line names it in an error, Invalid what a
    // menu prompt says to bad input, and Placeholder how usage shows the value.
    internal sealed record ValueKind<T>(InputParser<T> Parse, string Expected, string Invalid, string Placeholder);

    internal static class ValueKinds
    {
        public static readonly ValueKind<int> PositiveInt = new ValueKind<int>(InputParsing.TryPositiveInt, "a whole number of 1 or more", MenuPrompts.NotPositiveInteger, "N");

        public static readonly ValueKind<int> NonNegativeInt = new ValueKind<int>(InputParsing.TryNonNegativeInt, "a whole number of 0 or more", MenuPrompts.NotNonNegativeInteger, "N");

        public static readonly ValueKind<long> PositiveLong = new ValueKind<long>(InputParsing.TryPositiveLong, "a whole number of 1 or more", MenuPrompts.NotPositiveInteger, "N");

        public static readonly ValueKind<long> NonNegativeLong = new ValueKind<long>(InputParsing.TryNonNegativeLong, "a whole number of 0 or more", MenuPrompts.NotNonNegativeInteger, "N");

        public static readonly ValueKind<double> NonNegativeDouble = new ValueKind<double>(InputParsing.TryNonNegativeDouble, "a number of 0 or more", "Number was not 0 or more.", "X");

        public static readonly ValueKind<float> Probability = new ValueKind<float>(InputParsing.TryProbability, "a number between 0 and 1", "Number was not a rate between 0 and 1.", "X");

        public static readonly ValueKind<bool> Switch = new ValueKind<bool>(TrySwitch, "on or off", "Give on or off.", "on|off");

        public static readonly ValueKind<string> Text = new ValueKind<string>(TryText, "a value", "Give a value.", "TEXT");

        // Names or files separated by commas.
        public static readonly ValueKind<string[]> List = new ValueKind<string[]>(TryList, "names separated by commas", "Give names separated by commas.", "A,B");

        // One of the words given, ignoring case; the value is the word as declared.
        public static ValueKind<string> Choice(params string[] words) => new ValueKind<string>(
            (string input, out string value) => (value = words.FirstOrDefault(word => string.Equals(word, input.Trim(), StringComparison.OrdinalIgnoreCase))!) != null,
            string.Join(" or ", words), $"Give {string.Join(" or ", words)}.", string.Join("|", words));

        public static ValueKind<string> Named(string placeholder) => Text with { Placeholder = placeholder };

        private static bool TrySwitch(string input, out bool value)
        {
            value = string.Equals(input.Trim(), "on", StringComparison.OrdinalIgnoreCase);
            return value || string.Equals(input.Trim(), "off", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryText(string input, out string value)
        {
            value = input.Trim();
            return value.Length > 0;
        }

        private static bool TryList(string input, out string[] value)
        {
            value = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return value.Length > 0;
        }
    }
}
