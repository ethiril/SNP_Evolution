using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SnpEvolution.Cli
{
    // What an option or a menu prompt accepts. Expected is how the command line names it in an error, Invalid what a
    // menu prompt says to bad input, and Placeholder how usage shows the value.
    internal sealed record ValueKind<T>(InputParser<T> Parse, string Expected, string Invalid, string Placeholder)
    {
        // How a value is written so that Parse reads it back; the menu builds its command lines with it.
        public Func<T, string> Format { get; init; } = value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

        // The words a value can be, for a kind that takes one of a few; empty for any other.
        public IReadOnlyList<string> Words { get; init; } = Array.Empty<string>();
    }

    internal static class ValueKinds
    {
        public static readonly ValueKind<int> PositiveInt = new ValueKind<int>(InputParsing.TryPositiveInt, "a whole number of 1 or more", MenuPrompts.NotPositiveInteger, "N");

        public static readonly ValueKind<int> NonNegativeInt = new ValueKind<int>(InputParsing.TryNonNegativeInt, "a whole number of 0 or more", MenuPrompts.NotNonNegativeInteger, "N");

        public static readonly ValueKind<long> PositiveLong = new ValueKind<long>(InputParsing.TryPositiveLong, "a whole number of 1 or more", MenuPrompts.NotPositiveInteger, "N");

        public static readonly ValueKind<long> NonNegativeLong = new ValueKind<long>(InputParsing.TryNonNegativeLong, "a whole number of 0 or more", MenuPrompts.NotNonNegativeInteger, "N");

        public static readonly ValueKind<double> NonNegativeDouble = new ValueKind<double>(InputParsing.TryNonNegativeDouble, "a number of 0 or more", "Number was not 0 or more.", "X");

        public static readonly ValueKind<float> Probability = new ValueKind<float>(InputParsing.TryProbability, "a number between 0 and 1", "Number was not a rate between 0 and 1.", "X");

        public static readonly ValueKind<bool> Switch = Words(("on", true), ("off", false));

        public static readonly ValueKind<string> Text = new ValueKind<string>(TryText, "a value", "Give a value.", "TEXT");

        // Names or files separated by commas.
        public static readonly ValueKind<string[]> List = new ValueKind<string[]>(TryList, "names separated by commas", "Give names separated by commas.", "A,B")
        {
            Format = values => string.Join(",", values),
        };

        // One of the words given, ignoring case; the value is the word as declared.
        public static ValueKind<string> Choice(params string[] words) => Words(words.Select(word => (word, word)).ToArray());

        // One of the words given, ignoring case, read as the value it stands for.
        public static ValueKind<T> Words<T>(params (string Word, T Value)[] words)
        {
            string expected = string.Join(" or ", words.Select(each => each.Word));
            return new ValueKind<T>((string input, out T value) =>
            {
                int index = Array.FindIndex(words, each => string.Equals(each.Word, input.Trim(), StringComparison.OrdinalIgnoreCase));
                value = index >= 0 ? words[index].Value : default!;
                return index >= 0;
            }, expected, $"Give {expected}.", string.Join("|", words.Select(each => each.Word)))
            {
                Format = value => words.First(each => Equals(each.Value, value)).Word,
                Words = words.Select(each => each.Word).ToList(),
            };
        }

        // One of the entries, any whose name contains the text, or the one named exactly; written as its name.
        public static ValueKind<T> Named<T>(IReadOnlyList<T> entries, Func<T, string> name, string expected) where T : class =>
            new ValueKind<T>((string input, out T value) => (value = Application.Catalog.Matching(entries, name, input.Trim()).FirstOrDefault()!) != null,
                expected, $"Give {expected}.", "NAME")
            {
                Format = name,
            };

        // One of the enum's values, by its name in lower case.
        public static ValueKind<TEnum> Enum<TEnum>() where TEnum : struct, System.Enum =>
            Words(System.Enum.GetValues<TEnum>().Select(value => (value.ToString().ToLowerInvariant(), value)).ToArray());

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
