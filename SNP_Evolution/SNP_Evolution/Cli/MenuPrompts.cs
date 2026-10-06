using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Cli
{
    // The prompts every settings page asks with.
    internal static class MenuPrompts
    {
        internal const string NotPositiveInteger = "Number was not a positive integer.";

        internal static string Automatic(int value) => value > 0 ? value.ToString() : "automatic";

        // Returns current when the user goes back.
        internal static CatalogEntry<TContext, T> ChooseEntry<TContext, T>(Settings settings, string message, IReadOnlyList<CatalogEntry<TContext, T>> entries, CatalogEntry<TContext, T> current) =>
            ConsoleUi.Choose(settings, message, entries.Select(entry => entry.Name).ToList(), entries.ToList().IndexOf(current)) is int choice ? entries[choice] : current;

        internal static TEnum ChooseEnum<TEnum>(Settings settings, string message, TEnum current) where TEnum : struct, Enum
        {
            TEnum[] values = Enum.GetValues<TEnum>();
            return ConsoleUi.Choose(settings, message, values.Select(value => value.ToString()).ToList(), Array.IndexOf(values, current)) is int choice ? values[choice] : current;
        }

        internal static void PromptFor<T>(string request, string invalidMessage, InputParser<T> parse, Action<T> assign) =>
            ConsoleUi.PromptUntilAccepted(request, invalidMessage, input =>
            {
                if (!parse(input, out T value))
                {
                    return false;
                }
                assign(value);
                return true;
            });
    }
}
