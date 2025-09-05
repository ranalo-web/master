using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace Ranalo.Woocommece.Api.Services
{
    public class NewtonsoftValueAsStringConverter : Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(string);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                return reader.Value?.ToString();
            }

            // For arrays or objects, load as JToken and return raw JSON
            var token = JToken.Load(reader);
            return token.ToString(Formatting.None);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
    }
}
