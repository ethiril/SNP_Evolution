using System.Collections.Generic;
using System.Globalization;

namespace SnpEvolution.Cli
{
    internal delegate bool InputParser<T>(string input, out T value);

    internal static class InputParsing
    {
        public static bool TryPositiveInt(string input, out int value) =>
            int.TryParse(input, out value) && value > 0;

        public static bool TryProbability(string input, out float value) =>
            float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0 && value <= 1;

        public static bool TryIntegerSet(string input, out List<int> values)
        {
            values = new List<int>();
            foreach (string part in input.Split(','))
            {
                if (!int.TryParse(part, out int value))
                {
                    return false;
                }
                values.Add(value);
            }
            return true;
        }
    }
}
