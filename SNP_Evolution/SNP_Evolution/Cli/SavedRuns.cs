using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;

namespace SnpEvolution.Cli
{
    // The network an evolution run starts from.
    internal enum RunStart
    {
        Scratch,
        NaturalNumbers,
        EvenNumbers,
    }

    // A run's settings and starting point, kept so the run can be done again or its settings loaded later. Outcome
    // says how it went the time it was saved.
    internal sealed record SavedRun(string Name, DateTime SavedAt, RunStart Start, string FileStem, string Outcome, Settings Settings)
    {
        public string Summary
        {
            get
            {
                string task = Settings.Task == Catalog.TargetTask ? $"{Settings.Target.Kind} {Settings.Target}" : Settings.Task.Name;
                string start = Start == RunStart.Scratch ? "" : $", from the {(Start == RunStart.NaturalNumbers ? "natural" : "even")} numbers network";
                return $"{task}{start} | {SavedAt:yyyy-MM-dd HH:mm} | {Outcome}";
            }
        }
    }

    // Saved runs in one JSON file, newest first. Every settable setting is stored, so new settings are kept without
    // changes here; catalog choices are stored by name, and a name no longer offered falls back to the default.
    internal sealed class SavedRuns
    {
        public static readonly SavedRuns Default = new SavedRuns(System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SNP_Evolution", "saved-runs.json"));

        private static readonly JsonSerializerSettings JsonSettings = SavedRunSettings();
        public SavedRuns(string path)
        {
            Path = path;
        }

        public string Path { get; }

        // Empty when there is no file yet or it cannot be read.
        public IReadOnlyList<SavedRun> Load()
        {
            try
            {
                return File.Exists(Path)
                    ? JsonConvert.DeserializeObject<List<SavedRun>>(File.ReadAllText(Path), JsonSettings) ?? new List<SavedRun>()
                    : new List<SavedRun>();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is JsonException)
            {
                Console.WriteLine(exception.Message);
                return new List<SavedRun>();
            }
        }

        // Adds the run first, replacing any saved run with the same name.
        public void Add(SavedRun run) =>
            Write(new[] { run }.Concat(Load().Where(saved => !SameName(saved, run.Name))));

        public void Remove(string name) => Write(Load().Where(saved => !SameName(saved, name)));

        // The distinct targets of the saved runs, each with the newest run that used it.
        public IReadOnlyList<SavedRun> Targets() =>
            Load().Where(saved => saved.Settings.Task == Catalog.TargetTask)
                .GroupBy(saved => (saved.Settings.Target.Kind, saved.Settings.Target.ToString()))
                .Select(group => group.First())
                .ToList();

        private static bool SameName(SavedRun run, string name) => string.Equals(run.Name, name, StringComparison.OrdinalIgnoreCase);

        private void Write(IEnumerable<SavedRun> runs)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                File.WriteAllText(Path, JsonConvert.SerializeObject(runs.ToList(), JsonSettings));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Console.WriteLine(exception.Message);
            }
        }

        // Leaves out the settings worked out from others, such as the selected task or simulation options.
        private static JsonSerializerSettings SavedRunSettings()
        {
            JsonSerializerSettings settings = Json.Settings(new SettableOnlyResolver());
            settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            settings.Converters.Add(new StringEnumConverter());
            settings.Converters.Add(new CatalogEntryConverter());
            return settings;
        }

        private sealed class SettableOnlyResolver : Json.Resolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) =>
                base.CreateProperties(type, memberSerialization).Where(property => property.Writable || type != typeof(Settings)).ToList();
        }

        private sealed class CatalogEntryConverter : JsonConverter
        {
            private static readonly Dictionary<Type, (IEnumerable<object> Entries, object Default)> Catalogs = new()
            {
                [typeof(CatalogEntry<Settings, BenchmarkTask>)] = (Catalog.Tasks, Catalog.Tasks[0]),
                [typeof(CatalogEntry<Settings, ISimulationEngine>)] = (Catalog.Engines, Catalog.Engines[0]),
                [typeof(CatalogEntry<Settings, IFitnessFunction>)] = (Catalog.FitnessFunctions, Catalog.FitnessFunctions[0]),
                [typeof(CatalogEntry<EvolutionContext, IGeneticAlgorithm>)] = (Catalog.Algorithms, Catalog.StructuralDefault),
            };

            public override bool CanConvert(Type objectType) => Catalogs.ContainsKey(objectType);

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) => writer.WriteValue(value == null ? null : NameOf(value));

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                (IEnumerable<object> entries, object fallback) = Catalogs[objectType];
                return entries.FirstOrDefault(entry => NameOf(entry) == reader.Value as string) ?? fallback;
            }

            private static string NameOf(object entry) => (string)entry.GetType().GetProperty("Name")!.GetValue(entry)!;
        }
    }
}
