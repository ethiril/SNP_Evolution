using System.Globalization;

namespace SnpEvolution.Cli
{
    internal delegate bool InputParser<T>(string input, out T value);

    internal static class InputParsing
    {
        public static bool TryPositiveInt(string input, out int value) =>
            int.TryParse(input, out value) && value > 0;

        public static bool TryNonNegativeInt(string input, out int value) =>
            int.TryParse(input, out value) && value >= 0;

        public static bool TryNonNegativeLong(string input, out long value) =>
            long.TryParse(input, out value) && value >= 0;

        public static bool TryPositiveLong(string input, out long value) =>
            long.TryParse(input, out value) && value > 0;

        public static bool TryNonNegativeDouble(string input, out double value) =>
            double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0;

        public static bool TryProbability(string input, out float value) =>
            float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0 && value <= 1;
    }
}
