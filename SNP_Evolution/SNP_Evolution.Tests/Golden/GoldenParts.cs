using SnpEvolution.Specs.Parts;

namespace SnpEvolution.Tests.Golden
{
    internal static class GoldenParts
    {
        public static IEnumerable<(string Name, Part Part)> All() =>
            new[] { "parts", "parts-profile" }
                .SelectMany(folder => Directory.GetFiles(Path.Combine(RepositoryFiles.Root, folder), "*.json").Select(Path.GetFileName).Order(StringComparer.Ordinal)
                    .Select(file => ($"{folder}/{file}", RepositoryFiles.Part(folder, file!))))
                .Concat(HandBuiltParts.All().Select(part => ($"hand-built {part.Contract.Name}", part)));
    }
}
