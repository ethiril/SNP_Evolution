using System;
using Newtonsoft.Json;
using SnpEvolution.Evolution.Parts;

namespace SnpEvolution.Storage
{
    // Writes why a check stopped as the text the verify command prints, as part files always held it.
    public sealed class StopReasonJsonConverter : JsonConverter<StopReason>
    {
        public override StopReason ReadJson(JsonReader reader, Type objectType, StopReason? existingValue, bool hasExistingValue, JsonSerializer serializer) =>
            reader.Value is string text ? StopReason.Parse(text) : throw new JsonSerializationException($"Stopped must be text, not '{reader.Value}'.");

        public override void WriteJson(JsonWriter writer, StopReason? value, JsonSerializer serializer) => writer.WriteValue(value?.ToString());
    }
}
