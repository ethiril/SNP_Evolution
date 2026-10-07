using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SnpEvolution.Model;

namespace SnpEvolution.Storage
{
    // The JSON everything is saved as: networks, parts, contracts, NIR descriptions and saved runs. The converters the
    // saved types need are added here rather than named on the types, so the model does not depend on storage: spike
    // counts, and the reason a bounded check stopped.
    public static class Json
    {
        // A resolver caches what it learns of each type, so one is shared.
        private static readonly Resolver Shared = new Resolver();

        private static readonly JsonSerializerSettings Default = Settings();

        public static JsonSerializerSettings Settings(Resolver? resolver = null) =>
            new JsonSerializerSettings { Formatting = Formatting.Indented, ContractResolver = resolver ?? Shared, Converters = { new StopReasonJsonConverter() } };

        public static string Write(object value) => JsonConvert.SerializeObject(value, Default);

        // Null when the JSON holds nothing; throws JsonException when it is malformed.
        public static T? Read<T>(string json) => JsonConvert.DeserializeObject<T>(json, Default);

        // A neuron's spike count is written as a number, and read from the "aaa" strings earlier versions saved too.
        public class Resolver : DefaultContractResolver
        {
            private const string SpikeCount = "SpikeCount";

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);
                if (member.DeclaringType == typeof(Neuron) && property.PropertyName == SpikeCount)
                {
                    property.Converter = new SpikeCountJsonConverter();
                }
                return property;
            }

            // A neuron is read through its constructor, whose spike count needs the converter too.
            protected override JsonProperty CreatePropertyFromConstructorParameter(JsonProperty? matchingMemberProperty, ParameterInfo parameterInfo)
            {
                JsonProperty property = base.CreatePropertyFromConstructorParameter(matchingMemberProperty, parameterInfo);
                if (parameterInfo.Member.DeclaringType == typeof(Neuron) && property.PropertyName == SpikeCount)
                {
                    property.Converter = new SpikeCountJsonConverter();
                }
                return property;
            }
        }
    }
}
