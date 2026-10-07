using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // The prompts every settings page asks with.
    internal static class MenuPrompts
    {
        internal const string NotPositiveInteger = "Number was not a positive integer.";
        internal const string NotNonNegativeInteger = "Number was not a whole number of 0 or more.";

        internal static string Automatic(int value) => value > 0 ? value.ToString() : "automatic";

        // Returns current when the user goes back.
        internal static CatalogEntry<TContext, T> ChooseEntry<TContext, T>(Settings settings, string message, IReadOnlyList<CatalogEntry<TContext, T>> entries, CatalogEntry<TContext, T> current) =>
            Choose(settings, message, entries, current, entry => entry.Name);

        internal static T Choose<T>(Settings settings, string message, IReadOnlyList<T> entries, T current, Func<T, string> name) =>
            ConsoleUi.Choose(settings, message, entries.Select(name).ToList(), entries.ToList().IndexOf(current)) is int choice ? entries[choice] : current;

        internal static void PromptFor<T>(string request, string invalidMessage, InputParser<T> parse, Action<T> assign) =>
            ConsoleInput.PromptUntilAccepted(request, invalidMessage, input =>
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
