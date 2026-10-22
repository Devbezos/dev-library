using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DevClient.Data
{
    /// <summary>
    /// Parses JSON into JTokens the way System.Text.Json's JsonDocument did: date-looking strings stay strings. Newtonsoft's
    /// own JToken.Parse turns them into dates, which changes their type and how they print.
    /// </summary>
    public static class RawJson
    {
        public static JToken Parse(string json)
        {
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None };
            var token = JToken.Load(reader);
            while (reader.Read())
            {
                if (reader.TokenType != JsonToken.Comment)
                    throw new JsonReaderException("Additional text found in JSON after the end of the value.");
            }
            return token;
        }

        /// <summary>A string property, or null when it's missing or not a JSON string (JsonElement's ValueKind check).</summary>
        public static string? Str(this JToken? token, string name) =>
            token is JObject obj && obj[name] is { Type: JTokenType.String } value ? value.Value<string>() : null;
    }
}
