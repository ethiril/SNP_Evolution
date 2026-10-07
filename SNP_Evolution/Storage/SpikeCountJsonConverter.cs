using System;
using System.Linq;
using Newtonsoft.Json;

namespace SnpEvolution.Storage
{
    // Writes spike counts as numbers, and still reads the "aaa" strings that earlier versions saved.
    public sealed class SpikeCountJsonConverter : JsonConverter<long>
    {
        public override long ReadJson(JsonReader reader, Type objectType, long existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer when Convert.ToInt64(reader.Value) is long count && count >= 0:
                    return count;
                case JsonToken.String when reader.Value is string spikes && spikes.All(spike => spike == 'a'):
                    return spikes.Length;
                default:
                    throw new JsonSerializationException($"SpikeCount must be a non-negative number or a run of 'a's, not '{reader.Value}'.");
            }
        }

        public override void WriteJson(JsonWriter writer, long value, JsonSerializer serializer) => writer.WriteValue(value);
    }
}
